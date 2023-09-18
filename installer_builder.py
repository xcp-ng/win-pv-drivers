from command_utils import TIME, PROG, perror, die, do_cmd, ALL_PROJECTS
import os
import shutil
import tempfile
from zipfile import ZipFile
from contextlib import contextmanager
from subprocess import PIPE, SubprocessError
import sys
import argparse
import logging
import branding

@contextmanager
def change_dir(directory: str, *args, **kwds):
    """
    Temporarily changes the current directory.

    Changes to a directory when entering the context, returns to
    the previous directory when exiting the context.

    Usage:

    >>> with change_dir("path/to/dir/"):
    >>>     do_stuff_in_new_directory()
    >>> do_stuff_in_previous_directory()

    Returns None.
    """

    def __chdir(path):
        os.chdir(path)
        logging.info("Changed working directory to %s" % os.path.abspath(path))

    prevdir = os.path.abspath(os.curdir)
    __chdir(directory)
    try:
        yield
    finally:
        __chdir(prevdir)

def create_installer_dep_directory() -> str:
    """Create the directory of dependencies that the installer build requires."""
    depdir = tempfile.mkdtemp(prefix="xen_installer_")
    print("Installer dependency directory: %s" % depdir)

    for proj in ALL_PROJECTS:
        winless = proj.split("win-")[1]
        olddir = os.path.join(proj, winless)
        newdir = os.path.join(depdir, winless)
        
        # Affichez les chemins pour le debugging
        print(f"Old directory: {olddir}")
        print(f"New directory: {newdir}")
        
        # Vérifiez si le dossier olddir existe avant de tenter de le copier
        if os.path.exists(olddir):
            shutil.copytree(olddir, newdir)
        else:
            print(f"Error: The directory {olddir} does not exist.")
            # Vous pourriez choisir de quitter le script ici avec une erreur

    shutil.copytree(os.path.join('win-installer', 'vmcleaner'), os.path.join(depdir, 'vmcleaner'))
    return depdir
    
def get_certname() -> str:
    """Returns the certificate name as defined by branding."""
    return "%s(test)-%s" % (branding.branding["manufacturer"], TIME)
    
def authenticode_thumbprint(file: str) -> str:
    """
    Return the x509 certificate thumbprint from an Authenticode file.
    
    Arguments:
        file - Path to authenticode file (for example, a .exe or .msi file).
    
    Returns the thumbprint as a string.
    """
    command = "powershell.exe (Get-AuthenticodeSignature -FilePath {}).SignerCertificate.Thumbprint"\
                    .format(file)
    return do_cmd(command, stdout=PIPE).stdout.strip().decode()

def certificate_thumbprint(cert: str) -> str:
    """
    Return the thumbprint from an x509 certificate.

    Arguments:
        cert - Path to the certificate file.

    Returns the thumbprint as a string.
    """
    command = ("powershell.exe (New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 '%s')"
               ".Thumbprint") % os.path.abspath(cert)
    return do_cmd(command, stdout=PIPE).stdout.strip().decode()

def validate_authenticode_cert(cert: str, authenticode_file: str) -> None:
    """
    Validate the certificate used to sign an authenticode file.

    Arguments:
        cert - The certificate for which you want to determine whether or not it was used to
               sign the authenticode file.
        authenticode_file: the Authenticode file to be tested.

    Return True if the authenticode file is signed by cert.  Otherwise, return False.
    """
    auth_thumb = authenticode_thumbprint(authenticode_file)
    cert_thumb = certificate_thumbprint(cert)
    print("Comparing thumbprints %s (%s) and %s (%s)" % (cert, cert_thumb, authenticode_file, auth_thumb))
    if auth_thumb != cert_thumb:
        die("{}'s thumbprint ({}) does not match {}'s thumbprint ({})".format(
            cert, cert_thumb, authenticode_file, auth_thumb))

def build_installer(debug: bool = False) -> None:
    """Build the installer and print out its location."""
    if not os.path.exists("win-installer"):
        die("Source directory 'win-installer' does not exist, has '%s fetch' been executed?" % PROG)

    depdir = create_installer_dep_directory()

    certname = get_certname()
    fname = "%s-%s.cer" % (branding.branding["manufacturer"], TIME)

    if debug:
        certfile = os.path.abspath(fname)
        makecert(certfile, certname)
    else:
        certfile = os.path.join("certs", fname)
        certmgr_remove(certname)
        if not certmgr_add(certfile):
            die(("Failed to add %s to cert store. If this is a test build, "
                 " be sure to use the --debug flag.") % certfile)

    with change_dir("win-installer"):
        ret = do_cmd("python build.py --local %s --sign %s" % (depdir, certname))
        if ret.returncode != 0:
            die("failed: python build.py --local %s --sign %s" % (depdir, certname))

    outdir = os.path.abspath("output")
    if not os.path.exists(outdir):
        os.mkdir(outdir)
    builddir = os.path.join("win-installer", "installer")

    for fname in ["managementagentx64.msi", "managementagentx86.msi", "Setup.exe"]:
        fpath = os.path.join(builddir, fname)
        validate_authenticode_cert(certfile, fpath)
        shutil.copy(fpath, outdir)

    newcert = os.path.join(outdir, os.path.basename(certfile))
    if debug:
        shutil.move(certfile, newcert)
    else:
        shutil.copy(certfile, newcert)

    zipname = "%s.zip" % os.path.basename(os.getcwd())
    zippath = os.path.join(outdir, zipname)
    with ZipFile(zippath, 'w') as zip:
        for f in os.listdir('output'):
            if f == zipname:
                continue
            zip.write(os.path.join('output', f), arcname=f)
    

    print("SUCCESS: the installer may be found here: %s" % outdir)
    print("Test Certificate File:", newcert)
    print("Test Certificate Name:", certname)
    print("All output files bundled into file:", zippath)

def makecert(filename: str, certname: str) -> None:
    """
    Create a test signing cert using makecert.exe.
    
    This cert will be used by win-installer to sign the installer and drivers,
    and the cert may used for testing on a test machine.

    Arguments:
        filename: the output file name
        certname: the name of the cert (i.e., the CN)

    Returns the path to the cert.
    """
    certmgr_remove(certname)
    do_run([
        os.path.join(os.environ["KIT"], "bin", "x64", "makecert.exe"),
        "-r", "-pe",
        "-ss", "my", # the Personal store, required by win-installer
        "-n", "CN=%s" % certname,
        "-eku", "1.3.6.1.5.5.7.3.3", filename
    ], env=os.environ.copy())


def certmgr_remove(certname: str, store: str = "my") -> None:
    """
    Remove a certificate from the Windows certificate store.

    Arguments
    ---------
        certname: the name of the certificate to remove.
        store: the certificate store that contains the certificate.
    """
    certmgr = os.path.join(os.environ["KIT"], "bin", "x64", "certmgr.exe")
    remove = certmgr + " -del -all -n %s -s -r currentUser -c %s" % (certname, store)
    try:
        do_cmd(remove, stdout=PIPE, stderr=PIPE)
    except SubprocessError as e:
        print("WARNING: %s" % str(e))


def certmgr_add(certfile: str, store: str = "my") -> bool:
    """
    Add certificate to the Windows certificate store.

    Arguments
    ---------
        certfile: the path to the certificate file
        store: the name of the certificate store

    Return True if successful, otherwise False.
    """
    certmgr = os.path.join(os.environ["KIT"], "bin", "x64", "certmgr.exe")
    add = certmgr + " -add %s -s -r currentUser %s" % (certfile, store)
    return do_cmd(add).returncode == 0
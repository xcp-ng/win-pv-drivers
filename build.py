#!/usr/bin/env python

import argparse
import logging
import os
import pprint
import shutil
import sys
import tempfile
import time
from contextlib import contextmanager
from subprocess import PIPE, SubprocessError, call, run, CompletedProcess
from typing import Iterable, NoReturn, Optional
from zipfile import ZipFile
import branding

TIME = time.time_ns()
DRIVES = [letter + ":" for letter in 'ABCDEFGHIJKLMNOPQRSTUVWXYZ']

PROG = os.path.basename(sys.argv[0])

def do_run(*args, **kwargs) -> CompletedProcess:
    args_string = ", ".join(pprint.pformat(x) for x in args)
    # Environments are very large, so lets not print those
    copy = kwargs.copy()
    copy.pop("env", None)
    kwargs_string = pprint.pformat(copy)
    run_string = "run(args={}, kwargs={}".format(args_string, kwargs_string)
    logging.info("Invoking: " + run_string)
    try:
        ret = run(*args, **kwargs)
    except SubprocessError as e:
        logging.info(
            "Invoked(status={}): {}".format(
                e.returncode,
                run_string
            )
        )
        raise e

    logging.info(
        "Invoked(status={}): {}".format(
            ret.returncode,
            run_string
        )
    )

    return ret


def perror(message) -> None:
    """Print an error message to stderr."""
    print('ERROR: ' + message, file=sys.stderr)
    logging.error(message)


def die(message) -> NoReturn:
    """Print an error message to stderr and exit with exit code 1."""
    perror(message)
    sys.exit(1)

def setup_env() -> None:
    """Setup environment variables used by the build system."""

    if not os.environ.get("BUILD_ENV", None):
        logging.debug("Enviroment variable BUILD_ENV not found")
        for drive in DRIVES:
            path = os.path.normpath(os.path.join(drive, "BuildEnv"))
            logging.debug(f"Searching for {path}")
            
            if os.path.exists(os.path.join(path, "SetupBuildEnv.cmd")):
                os.environ["BUILD_ENV"] = os.path.normpath(path)
                logging.debug(f'Environment variable BUILD_ENV set to {os.environ["BUILD_ENV"]}')
                break

    if not os.environ.get("VS", None):
        logging.debug("Environment variable VS not found")
        for directory, _, _ in os.walk(os.path.join('C:', '/')):
            path = os.path.join(directory, "VC", "vcvarsall.bat")
            logging.debug(f"Searching for path {path}")
            if os.path.exists(path):
                os.environ["VS"] = os.path.normpath(directory)
                logging.debug(f'Environment variable VS set to {os.environ["VS"]}')
                break

    systemdir = os.path.join("C:", "/", "Program Files (x86)")
    if not os.environ.get("WIX", None):
        logging.debug("Environment variable WIX not found")
        logging.debug("Searching for directory with substring WiX Toolset")
        for directory, _, _ in os.walk(systemdir):
            if "WiX Toolset" in directory:
                os.environ["WIX"] = os.path.normpath(directory)
                break
        if "WIX" in os.environ:
            logging.info(f"Environment variable WIX set to {os.environ['WIX']}")

    if not os.environ.get("KIT", None):
        logging.debug("Environment variable KIT not found")
        logging.debug("Searching for highest version path C:/Program Files (x86)/Windows Kits/X.Y/")

        # Find highest version from 0.0 to 100.100
        for major in range(100):
            for minor in range(100):
                version = f"{major}.{minor}"
                path = os.path.normpath(os.path.join(systemdir, "Windows Kits", version))
                if os.path.exists(path):
                    os.environ["KIT"] = path
        if "KIT" in os.environ:
            logging.info(f"Environment variable KIT set to {os.environ['KIT']}")

def check_env() -> None:
    """Check that all required enviornment variables are defined."""
    vars = set([
        "BUILD_ENV",
        "WIX",
        "KIT",
        "VS",
    ])

    missing = vars - set(os.environ.keys())
    if missing:
        die("Please set the following environment variables: %s" % ', '.join(missing))
    else:
        logging.info("All environment variables found.")
        for var in sorted(list(vars)):
            logging.info("%s = %s" % (var, os.environ[var]))


urls = [
    "https://www.github.com/xcp-ng/win-xenbus.git",
    "https://www.github.com/xcp-ng/win-xenguestagent.git",
    "https://www.github.com/xcp-ng/win-xeniface.git",
    "https://www.github.com/xcp-ng/win-xenvif.git",
    "https://www.github.com/xcp-ng/win-xennet.git",
    "https://www.github.com/xcp-ng/win-xenvbd.git",
]


def url_to_simple_name(url) -> str:
    """
    Reduce URL to name of git repo.

    For example, "https://www.github.com/xcp-ng/win-xenbus.git" becomes "win-xenbus"

    Returns the name of the git repo with no .git extension.
    """
    return os.path.basename(url).split('.git')[0]


ALL_PROJECTS = [url_to_simple_name(url) for url in urls]


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


def fetch() -> None:
    """Fetch all repos."""

    for url in urls:
        do_run(["git", "clone", url])

    # Also download the installer
    do_run(["git", "clone", "https://github.com/xcp-ng/win-installer.git"])


def check_projects(projects: Iterable[str]) -> None:
    """Check that a list of projects is valid."""
    global ALL_PROJECTS

    rem = set(projects) - set(ALL_PROJECTS + ["win-installer"])
    if rem:
        die("project(s) %s not valid.  Options are: %s" % (', '.join(rem), ALL_PROJECTS))


def ewdk_cmd(cmd: str, *args, **kwargs) -> CompletedProcess:
    """
    Execute a command inside a EWDK build environment.

    Returns a CompletedProcess.
    """
    build_env = os.path.normpath(os.path.join(
        os.environ["BUILD_ENV"],
        "SetupBuildEnv.cmd")
    )
    kwargs['shell'] = True
    return do_run(['cmd.exe', '/C', 'call %s && %s' % (build_env, cmd)], env=os.environ.copy(), *args, **kwargs)


def do_cmd(cmd: str, *args, **kwargs) -> CompletedProcess:
    """
    Execute a simple command.

    Returns a CompletedProcess.
    """
    return do_run(cmd.split(), env=os.environ.copy(), *args, **kwargs)


def build(projects: Iterable[str], checked: bool) -> None:
    """Build all source repos if projects is empty, otherwise build only those repos found in projects."""
    global ALL_PROJECTS    

    check_projects(projects)

    for i, dirname in enumerate(ALL_PROJECTS):
        assert os.path.exists(dirname), \
            "Source directory %s does not exist, has '%s fetch' been executed?" % (dirname, PROG)

        if dirname not in projects:
            continue

        if "win-xenguestagent" in dirname:
            exec_cmd = do_cmd
        else:
            exec_cmd = ewdk_cmd

        with change_dir(dirname):
            py_script = os.path.join(os.curdir, "build.py")
            ps_script = os.path.join(os.curdir, "build.ps1")
            buildarg = "checked" if checked else "free"
            # TODO: make toggleable the "checked" option by a --debug flag
            if os.path.exists(py_script):
                p = exec_cmd("python %s %s" % (py_script, buildarg))
            elif os.path.exists(ps_script):
                p = exec_cmd("powershell -file %s %s" % (ps_script, buildarg))
            else:
                die("No %s or %s found" % (ps_script, py_script))

            if p and p.returncode != 0:
                die("Built %s projects, but building %s failed. Stopped." % (i, dirname))


def create_installer_dep_directory() -> str:
    """Create the directory of dependencies that the installer build requires."""
    depdir = tempfile.mkdtemp(prefix="xen_installer_")
    print("Installer dependency directory: %s" % depdir)

    for proj in ALL_PROJECTS:
        winless = proj.split("win-")[1]
        olddir = os.path.join(proj, winless)
        newdir = os.path.join(depdir, winless)
        shutil.copytree(olddir, newdir)

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


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="The Windows PV drivers builder.")
    parser.set_defaults(action=None)

    parser.add_argument("--loglevel", choices=["DEBUG", "INFO", "ERROR"])

    subparsers = parser.add_subparsers()
    fetch_parser = subparsers.add_parser("fetch", help="Fetch all source repos.")
    fetch_parser.set_defaults(action="fetch")

    build_parser = subparsers.add_parser("build", help="Build all source repos.")
    build_parser.set_defaults(action="build")
    build_parser.add_argument("projects", nargs="*", choices=ALL_PROJECTS + ["win-installer", []], help="The projects to build.")
    build_parser.add_argument("--debug", "-d", action="store_true", help="Build projects with debug config.")

    args = parser.parse_args()

    logging.basicConfig(level={
            "INFO": logging.INFO,
            "DEBUG": logging.DEBUG,
            "ERROR": logging.ERROR,
        }.get(args.loglevel, logging.INFO)
    )

    if args.action == "fetch":
        fetch()
    elif args.action == "build":
        setup_env()
        check_env()
        build_all = not args.projects
        build(ALL_PROJECTS if build_all else args.projects, checked=args.debug)
        if "win-installer" in args.projects or build_all:
            build_installer(args.debug)
    else:
        parser.print_help()
        sys.exit(1)

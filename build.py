#!/usr/bin/env python

import argparse
import logging
import os
import pprint
import shutil
import sys
import tempfile
import time
import subprocess
from typing import Iterable
from contextlib import contextmanager
from zipfile import ZipFile
import branding
from command_utils import (
    do_run, 
    is_wix_dotnet_tool_installed,
    find_file_in_drives,
    is_valid_build_env,
    setup_env,
    check_env,
    do_cmd,
    get_build_env_config,
    build_env_cmd
)

TIME = time.time_ns()
PROG = os.path.basename(sys.argv[0])

def perror(message) -> None:
    print('ERROR: ' + message, file=sys.stderr)
    logging.error(message)

def die(message):
    perror(message)
    sys.exit(1)

urls = [
    "https://www.github.com/xcp-ng/win-xenbus.git",
    "https://www.github.com/xcp-ng/win-xeniface.git",
    "https://www.github.com/xcp-ng/win-xenvif.git",
    "https://www.github.com/xcp-ng/win-xennet.git",
    "https://www.github.com/xcp-ng/win-xenvbd.git",
    "https://www.github.com/xcp-ng/win-xenguestagent.git",
]

def url_to_simple_name(url) -> str:
    return os.path.basename(url).split('.git')[0]

ALL_PROJECTS = [url_to_simple_name(url) for url in urls]

@contextmanager
def change_dir(directory: str, *args, **kwds):
    logging.info("Changing working directory to %s" % directory)
    def __chdir(path):
        os.chdir(path)
        logging.info("Changed working directory to %s" % os.path.abspath(os.curdir))

    prevdir = os.path.abspath(os.curdir)
    logging.info("Previous working directory was %s" % prevdir)
    __chdir(directory)
    try:
        yield
    finally:
        __chdir(prevdir)
        logging.info("Returned to previous directory %s" % os.path.abspath(os.curdir))

def fetch() -> None:
    win_pv_drivers_branch = subprocess.check_output(["git", "rev-parse", "--abbrev-ref", "HEAD"]).strip().decode()
    win_pv_drivers_url = subprocess.check_output(["git", "remote", "get-url", "origin"]).strip().decode() 
    win_pv_drivers_repo_name = url_to_simple_name(win_pv_drivers_url)
    if win_pv_drivers_branch.endswith(win_pv_drivers_repo_name):
        prefix = win_pv_drivers_branch[:-len(win_pv_drivers_repo_name)]
    else:
        prefix = None

    for url in urls:
        repo_name = url_to_simple_name(url)
        branch_name = f"{prefix}{repo_name}" if prefix else "master"
        do_run(["git", "clone", "-b", branch_name, url])
    do_run(["git", "clone", "https://github.com/xcp-ng/win-installer.git"])

def check_projects(projects: Iterable[str]) -> None:
    global ALL_PROJECTS
    rem = set(projects) - set(ALL_PROJECTS + ["win-installer"])
    if rem:
        die("project(s) %s not valid.  Options are: %s" % (', '.join(rem), ALL_PROJECTS))
        
def build(projects: Iterable[str], checked: bool, sdv: bool) -> None:
    """Build all source repos if projects is empty, otherwise build only those repos found in projects."""
    global ALL_PROJECTS 
    
    check_projects(projects)

    ps_script = os.path.join(os.getcwd(), "build.ps1")
    ps_script = '"' + ps_script + '"' if ' ' in ps_script else ps_script
    buildarg = "checked" if checked else "free"
    sdvarg = "sdv" if sdv else ""

    for i, dirname in enumerate(ALL_PROJECTS):
        assert os.path.exists(dirname), \
            "Source directory %s does not exist, has '%s fetch' been executed?" % (dirname, PROG)

        if dirname not in projects:
            continue
            
        if "win-xenguestagent" in dirname:
            p = build_env_cmd(['python', os.path.join(dirname, 'build.py'), buildarg])
        else:
            p = build_env_cmd(['powershell', '-file', ps_script, '-RepoName', f'"{dirname}"', buildarg, sdvarg])

        if p and p.returncode != 0:
            die("Built %s projects, but building %s failed. Stopped." % (i, dirname))


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
    build_parser.add_argument("--sdv", action="store_true", help="Run SDV analysis.")

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
        build(ALL_PROJECTS if build_all else args.projects, checked=args.debug, sdv=args.sdv)
        if "win-installer" in args.projects or build_all:
            build_installer(args.debug)
    else:
        parser.print_help()
        sys.exit(1)

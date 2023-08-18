import logging
import os
import subprocess
import pprint
from subprocess import run, CompletedProcess, PIPE, SubprocessError
from typing import List

DRIVES = [letter + ":" for letter in 'ABCDEFGHIJKLMNOPQRSTUVWXYZ']
EWDK_FILE_PATH = "SetupBuildEnv.cmd"
VS_FILE_PATH = os.path.join("VC", "Auxiliary", "Build", "vcvarsall.bat")

def do_run(*args, **kwargs) -> CompletedProcess:
    args_string = ", ".join(pprint.pformat(x) for x in args)
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

def is_wix_dotnet_tool_installed() -> bool:
    logging.debug("Checking if WiX is installed as a .NET global tool")
    try:
        subprocess.run(["wix", "--version"], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        result = True
    except subprocess.CalledProcessError:
        result = False
    logging.debug(f"WiX is installed as a .NET global tool: {result}")
    return result

def find_file_in_drives(file_path):
    for drive in DRIVES:
        for directory, _, _ in os.walk(os.path.join(drive, '/')):
            path = os.path.join(directory, file_path)
            if os.path.exists(path):
                return os.path.normpath(directory)
    return None

def is_valid_build_env(path):
    return os.path.exists(os.path.join(path, EWDK_FILE_PATH)) or os.path.exists(os.path.join(path, VS_FILE_PATH))

def setup_env() -> None:
    if "BUILD_ENV" in os.environ:
        if not is_valid_build_env(os.environ["BUILD_ENV"]):
            logging.error(f'Environment variable BUILD_ENV does not point to a valid build environment: {os.environ["BUILD_ENV"]}')
            sys.exit(1)
    else:
        logging.debug("Environment variable BUILD_ENV not found")
        build_env_path = find_file_in_drives(EWDK_FILE_PATH)
        if build_env_path:
            os.environ["BUILD_ENV"] = os.path.join(build_env_path, EWDK_FILE_PATH)
        else:
            logging.debug("EWDK not found, searching for Visual Studio installation")
            vs_path = find_file_in_drives(VS_FILE_PATH)
            if vs_path:
                os.environ["BUILD_ENV"] = os.path.join(vs_path, VS_FILE_PATH)
            else:
                logging.error("Neither EWDK nor Visual Studio installation found.")
                sys.exit(1)
    if "BUILD_ENV" in os.environ:
        logging.debug(f'Environment variable BUILD_ENV set to {os.environ["BUILD_ENV"]}')
    if not os.environ.get("WIX", None) and not os.environ.get("WIX_DOTNET_TOOL", None):
        logging.debug("Environment variable WIX not found")
        if is_wix_dotnet_tool_installed():
            os.environ["WIX_DOTNET_TOOL"] = "1"
            logging.info("WiX is installed as a .NET global tool")
        else:
            logging.debug("WiX is not installed as a .NET global tool")
            wix_path = find_file_in_drives(os.path.join("wix", "candle.exe"))
            if wix_path:
                os.environ["WIX"] = wix_path
                logging.info(f'Environment variable WIX set to {os.environ["WIX"]}')
            else:
                logging.error("WiX not found")
                sys.exit(1)
    systemdir = os.path.join('C:', os.sep, 'Program Files (x86)')          
    if not os.environ.get("KIT", None):
        logging.debug("Environment variable KIT not found")
        logging.debug("Searching for highest version path C:/Program Files (x86)/Windows Kits/X.Y/")
        for major in range(100):
            for minor in range(100):
                version = f"{major}.{minor}"
                path = os.path.normpath(os.path.join(systemdir, "Windows Kits", version))
                if os.path.exists(path):
                    os.environ["KIT"] = path
        if "KIT" in os.environ:
            logging.info(f"Environment variable KIT set to {os.environ['KIT']}")
        else:
            logging.error("Windows Kits not found")
            sys.exit(1)

def check_env() -> None:
    vars = set([
        "BUILD_ENV",
        "KIT"
    ])
    missing = vars - set(os.environ.keys())
    if "WIX" not in os.environ and "WIX_DOTNET_TOOL" not in os.environ:
        vars.add("WIX")
    if missing:
        die("Please set the following environment variables: %s" % ', '.join(missing))
    else:
        logging.info("All environment variables found.")
        for var in sorted(list(vars)):
            logging.info("%s = %s" % (var, os.environ[var]))

def do_cmd(cmd: List[str], *args, **kwargs) -> CompletedProcess:
    return do_run(cmd, env=os.environ.copy(), *args, **kwargs)

def get_build_env_config():
    build_env = os.environ.get("BUILD_ENV", None)
    if not build_env:
        logging.error("Environment variable BUILD_ENV not found.")
        return None, None
    if VS_FILE_PATH in build_env:
        build_env_setup = build_env
        build_env_args = ["x86_amd64"]
    else:
        build_env_setup = os.path.normpath(build_env)
        build_env_args = []
    return build_env_setup, build_env_args

def build_env_cmd(cmd: List[str], *args, **kwargs) -> CompletedProcess:
    build_env_setup, build_env_args = get_build_env_config()
    if not build_env_setup:
        return
    cmd_str = ' '.join(cmd)
    build_env_setup = '"' + build_env_setup + '"' if ' ' in build_env_setup else build_env_setup
    command = ' '.join(['cmd.exe', '/C', 'call', build_env_setup] + build_env_args + ['&&'] + [cmd_str])
    return do_run(command, env=os.environ.copy(), *args, **kwargs)

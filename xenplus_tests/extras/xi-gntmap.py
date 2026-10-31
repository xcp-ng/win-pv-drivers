#!/usr/bin/env python3
"""
xi-gntmap.py: dom0 side of the xeniface "revoke ignored" reproduction.

Maps the given grant references of ONE guest read-write (libxengnttab), logs what the guest wrote, then keeps
stamping a recognisable 64-byte record over every mapped page until <run-dir>/stop appears or --max-seconds
elapse. The mapping is always released in `finally` and on SIGTERM/SIGINT/SIGHUP: a domain cannot be destroyed
while dom0 maps its pages.

Safety: refuses domid 0, more than 16 refs, and any domain whose xenstore /local/domain/<D>/vm does not end with
/vm/<vm-uuid>. Nothing is mapped before those checks pass. No other domain and no other process is touched.

Every log event is written to <run-dir>/log as a text line and printed to stdout as one JSON object per line
({"t": <epoch>, "event": <name>, "line": <text line>, ...}). Python 3.6 compatible.

--xenstore mode (instead of --refs): coordinates with the xenplus_tests G17 cases through the guest xenstore keys
/local/domain/<D>/data/xeniface-test/<token>/. Sequence: wait for guest-state=granted, read refs, map read-write, log
the guest markers, write partner-state=mapped; wait until guest-state starts with revoked: or cancelled:, then stamp
every 20 ms and write partner-state=stamped after 1 s; keep stamping until guest-state=done (or the cap); unmap and
write partner-state=unmapped. On failure partner-state=error:<text> is written best effort.
"""

import argparse
import ctypes
import json
import os
import re
import signal
import subprocess
import sys
import time

MAX_REFS = 16
MAX_SECONDS_CAP = 240
PAGE = 4096
RECORD = 64
PROT_READ = 1
PROT_WRITE = 2
TOKEN_RE = re.compile(r"^[A-Za-z0-9-]{1,32}$")
UUID_RE = re.compile(r"^[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}$")

_log_file = None
_stop_requested = False
_stop_path = None


def on_signal(signum, frame):
    global _stop_requested
    _stop_requested = True


def stop_requested():
    """A signal arrived, or <run-dir>/stop exists."""
    return _stop_requested or os.path.exists(_stop_path)


def emit(event, line, **extra):
    """Log one event: text line to the log file, JSON line to stdout. Both flushed."""
    _log_file.write(line + "\n")
    _log_file.flush()
    obj = {"t": round(time.time(), 3), "event": event, "line": line}
    obj.update(extra)
    sys.stdout.write(json.dumps(obj) + "\n")
    sys.stdout.flush()


def printable(data):
    return "".join(chr(b) if 32 <= b < 127 else "." for b in bytearray(data))


def parse_refs(text):
    refs = [int(r) for r in text.split(",") if r.strip()]
    if not refs or len(refs) > MAX_REFS or any(not 0 <= r <= 0xFFFFFFFF for r in refs):
        raise ValueError(
            "need 1 to %d refs in 0..0xFFFFFFFF, got %r" % (MAX_REFS, text)
        )
    return refs


def make_page(token, index, counter):
    rec = ("XIUAF:%s:p%d:c%d" % (token, index, counter)).encode()[: RECORD - 1]
    rec = rec + b" " * (RECORD - 1 - len(rec)) + b"\n"
    return rec * (PAGE // RECORD)


def load_lib():
    lib = ctypes.CDLL("libxengnttab.so.1", use_errno=True)
    lib.xengnttab_open.restype = ctypes.c_void_p
    lib.xengnttab_open.argtypes = [ctypes.c_void_p, ctypes.c_uint]
    lib.xengnttab_close.restype = ctypes.c_int
    lib.xengnttab_close.argtypes = [ctypes.c_void_p]
    lib.xengnttab_map_domain_grant_refs.restype = ctypes.c_void_p
    lib.xengnttab_map_domain_grant_refs.argtypes = [
        ctypes.c_void_p,
        ctypes.c_uint32,
        ctypes.c_uint32,
        ctypes.POINTER(ctypes.c_uint32),
        ctypes.c_int,
    ]
    lib.xengnttab_unmap.restype = ctypes.c_int
    lib.xengnttab_unmap.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_uint32]
    return lib


def check_domain(domid, vm_uuid):
    out = (
        subprocess.check_output(["xenstore-read", "/local/domain/%d/vm" % domid])
        .decode()
        .strip()
    )
    if not out.endswith("/vm/" + vm_uuid):
        raise SystemExit(
            "domid %d belongs to %r, not VM %s: refusing to map" % (domid, out, vm_uuid)
        )


def xs_path(domid, token, key):
    return "/local/domain/%d/data/xeniface-test/%s/%s" % (domid, token, key)


def xs_read(domid, token, key):
    """Returns the key value, or None when it does not exist."""
    r = subprocess.run(
        ["xenstore-read", xs_path(domid, token, key)],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    return r.stdout.decode(errors="replace").strip() if r.returncode == 0 else None


def xs_write(domid, token, key, value):
    subprocess.check_call(["xenstore-write", xs_path(domid, token, key), value])


def xs_try_write(domid, token, key, value):
    """Best effort: the guest may already be gone."""
    subprocess.call(["xenstore-write", xs_path(domid, token, key), value])


def xs_wait(domid, token, key, accept, deadline, what):
    """Polls a key every 100 ms until accept(value) is true; raises RuntimeError on the deadline or a signal."""
    while True:
        value = xs_read(domid, token, key)
        if value is not None and accept(value):
            return value
        if stop_requested():
            raise RuntimeError("interrupted while waiting for %s" % what)
        if time.monotonic() >= deadline:
            raise RuntimeError("timeout waiting for %s (last %r)" % (what, value))
        time.sleep(0.1)


def run(args, refs, deadline):
    """Maps, logs the guest markers, stamps, always unmaps. With --xenstore, refs and pacing come from the guest."""
    domid, token, xs = args.domid, args.token, args.xenstore
    lib = load_lib()
    xgt = addr = None
    stamps = 0
    try:
        if xs:
            xs_wait(
                domid,
                token,
                "guest-state",
                lambda v: v == "granted",
                deadline,
                "guest-state=granted",
            )
            refs = parse_refs(xs_read(domid, token, "refs") or "")
        xgt = lib.xengnttab_open(None, 0)
        if not xgt:
            raise RuntimeError("xengnttab_open failed (errno %d)" % ctypes.get_errno())
        addr = lib.xengnttab_map_domain_grant_refs(
            xgt,
            len(refs),
            domid,
            (ctypes.c_uint32 * len(refs))(*refs),
            PROT_READ | PROT_WRITE,
        )
        if not addr:
            raise RuntimeError(
                "map of refs %s from domid %d failed (errno %d)"
                % (refs, domid, ctypes.get_errno())
            )
        emit(
            "mapped",
            "mapped refs=%s domid=%d count=%d"
            % (",".join(str(r) for r in refs), domid, len(refs)),
            refs=refs,
            domid=domid,
        )
        for i in range(len(refs)):
            data = printable(ctypes.string_at(addr + i * PAGE, 48))
            emit(
                "guest-marker",
                "guest-marker page=%d data=%s" % (i, data),
                page=i,
                data=data,
            )

        if xs:
            xs_write(domid, token, "partner-domid", "0")
            xs_write(domid, token, "partner-state", "mapped")
            state = xs_wait(
                domid,
                token,
                "guest-state",
                lambda v: (
                    v.startswith("revoked:")
                    or v.startswith("cancelled:")
                    or v == "done"
                ),
                deadline,
                "guest-state revoked:/cancelled:",
            )
            emit("guest-state", "guest-state=%s" % state, state=state)

        emit("stamping", "stamping")
        stamp_start = time.monotonic()
        reported = False
        while not stop_requested() and time.monotonic() < deadline:
            for i in range(len(refs)):
                ctypes.memmove(addr + i * PAGE, make_page(token, i, stamps), PAGE)
            stamps += 1
            if xs and not reported and time.monotonic() - stamp_start >= 1.0:
                xs_write(domid, token, "partner-state", "stamped")
                emit("stamped", "partner-state=stamped")
                reported = True
            if xs and reported and xs_read(domid, token, "guest-state") == "done":
                break
            time.sleep(0.02)
        emit("stamp-count", "stamp-count=%d" % stamps, count=stamps)
    except Exception as exc:
        text = str(exc).replace("\n", " ")[:80]
        emit("error", "error: %s" % text)
        if xs:
            xs_try_write(domid, token, "partner-state", "error:" + text)
        raise
    finally:
        if addr:
            rc = lib.xengnttab_unmap(xgt, addr, len(refs))
        if xgt:
            lib.xengnttab_close(xgt)
        if addr:
            emit("unmapped", "unmapped rc=%d" % rc, rc=rc)
            if xs:
                xs_try_write(domid, token, "partner-state", "unmapped")


def main():
    global _log_file, _stop_path
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[1])
    ap.add_argument("--domid", type=int, required=True)
    ap.add_argument("--vm-uuid", required=True)
    ap.add_argument("--refs", help="comma separated grant references (CLI-refs mode)")
    ap.add_argument(
        "--xenstore",
        action="store_true",
        help="take the refs from the guest xenstore keys and report through partner-state",
    )
    ap.add_argument("--token", required=True)
    ap.add_argument("--run-dir", default="/run/xi-gntmap")
    ap.add_argument("--max-seconds", type=int, default=MAX_SECONDS_CAP)
    args = ap.parse_args()

    if args.domid <= 0:
        raise SystemExit("refusing domid %d" % args.domid)
    if not UUID_RE.match(args.vm_uuid):
        raise SystemExit("--vm-uuid is not a UUID")
    if not TOKEN_RE.match(args.token):
        raise SystemExit("--token must match %s" % TOKEN_RE.pattern)
    if args.xenstore == bool(args.refs):
        raise SystemExit("give exactly one of --refs and --xenstore")
    refs = []
    if args.refs:
        try:
            refs = parse_refs(args.refs)
        except ValueError as exc:
            raise SystemExit("--refs: %s" % exc)
    max_seconds = max(1, min(args.max_seconds, MAX_SECONDS_CAP))

    check_domain(args.domid, args.vm_uuid.lower())

    os.makedirs(args.run_dir, exist_ok=True)
    _stop_path = os.path.join(args.run_dir, "stop")
    with open(os.path.join(args.run_dir, "pid"), "w") as f:
        f.write("%d\n" % os.getpid())
    for sig in (signal.SIGTERM, signal.SIGINT, signal.SIGHUP):
        signal.signal(sig, on_signal)

    with open(os.path.join(args.run_dir, "log"), "a") as _log_file:
        run(args, refs, time.monotonic() + max_seconds)
    return 0


if __name__ == "__main__":
    sys.exit(main())

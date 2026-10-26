#!/usr/bin/env python3
"""
Generate the voice-over with ElevenLabs, one file per line, from the markdown script.

Built to spend as few credits as possible:

  * DRY RUN BY DEFAULT. Without --go nothing is sent: it lists what would be generated,
    the characters it would cost, and (with an API key set) how many you have left.
  * A LEDGER (ledger.json, next to this file). Every generated line is recorded with a
    fingerprint of exactly what was sent: text, voice, model, settings, format, seed.
    A line whose fingerprint is unchanged is never generated again. Edit one line of the
    script, and only that line costs anything. Commit ledger.json so nobody on the team
    pays for a line somebody else already made.
  * STAGING. Files go to staging/ first, which Unity ignores (this folder ends in ~).
    Listen, then --install copies them into Assets/VoiceOver, overwriting the existing
    files of the same name — which keeps their GUIDs, so nothing in Unity needs rewiring.

Usage (from this folder):

    python elevenlabs_vo.py voices                  list your voices (free)
    python elevenlabs_vo.py test "Short sample."    one sample into staging/_test.mp3 (costs its characters)
    python elevenlabs_vo.py plan                    dry run: what would be generated, and the cost
    python elevenlabs_vo.py plan --only D5,N_A1     ...for some lines only
    python elevenlabs_vo.py plan --part tutorial    ...for one half of the script
    python elevenlabs_vo.py generate --go           generate what plan listed
    python elevenlabs_vo.py install                 copy staged files into Assets/VoiceOver

The API key is read from the ELEVENLABS_API_KEY environment variable and nowhere else.
Settings are in config.json (copy config.example.json). No third-party packages needed.

Delivery tags ([softly], [in awe]...) are in delivery.json, keyed by line id, with "*" as
the default. They are sent to ElevenLabs only - the script stays clean, and Unity never
sees them. Only eleven_v3 understands them; with any other model they are left off,
because older models can read the brackets out loud. Changing one line's tag regenerates
that line and nothing else.
"""

import argparse
import hashlib
import json
import os
import re
import shutil
import sys
import urllib.error
import urllib.request
from datetime import datetime

HERE = os.path.dirname(os.path.abspath(__file__))
VO_ROOT = os.path.dirname(HERE)                                 # Assets/VoiceOver
STAGING = os.path.join(HERE, "staging")
LEDGER = os.path.join(HERE, "ledger.json")
CONFIG = os.path.join(HERE, "config.json")
DELIVERY = os.path.join(HERE, "delivery.json")

API = "https://api.elevenlabs.io/v1"
VARIANTS = ("A", "B")


# -- The script -------------------------------------------------------------------------

class Line:
    def __init__(self, part, line_id, variant, text, role):
        self.part = part            # "Tutorial" or "Journey" - also the folder it goes in
        self.id = line_id
        self.variant = variant      # "" or "A" / "B"
        self.text = text
        self.role = role
        self.file = None            # file name it will be written as

    @property
    def key(self):
        return self.id + ("_" + self.variant if self.variant else "")


def clean(row):
    row = re.sub(r"\*\*\[v\d+\]\*\*|\[v\d+\]", "", row)      # stray change markers
    return re.sub(r"\s{2,}", " ", row).strip()


ID_RE = re.compile(r"^(?:N_)?[A-Za-z][A-Za-z0-9]*(?:_[A-Za-z0-9]+)*$")


def parse_script(path):
    """
    The same rules as VoiceOverTableTools_NEW.Parse in Unity. Every line is a row of a
    four-column table, | ID | When | Line | ≈ |, under "## Part 1" (the tutorial) or
    "## Part 2" (the journey). Everything else is notes for people and is skipped.
    """
    text = open(path, encoding="utf-8").read().replace("\r", "")
    if "## Part 2" not in text:
        sys.exit("No '## Part 2' in the script; cannot tell the tutorial from the journey.")

    lines = []
    part = None
    for raw in text.split("\n"):
        row = clean(raw)

        if row.startswith("## "):
            part = ("Tutorial" if row.startswith("## Part 1")
                    else "Journey" if row.startswith("## Part 2") else None)
            continue
        if not part or not row.startswith("|"):
            continue

        cells = [c.strip() for c in row.strip("|").split("|")]
        if len(cells) < 4:
            continue
        line_id, body = cells[0].strip("*`"), cells[2]
        if line_id == "ID" or not ID_RE.match(line_id) or not body:
            continue

        lines.append(Line(part, line_id, "", body, "nudge" if line_id.startswith("N_") else "narration"))
    return lines


# -- File names ---------------------------------------------------------------------------

def assign_files(lines):
    """
    Reuse the existing file for a line wherever there is one, so overwriting it keeps the
    GUID Unity already has wired up. Same matching as the Unity scanner: the longest line
    id that prefixes the name wins, a trailing _A/_B is the version, and a file with no
    version suffix on a line the script splits into versions belongs to version A.
    """
    by_part = {}
    for l in lines:
        by_part.setdefault(l.part, []).append(l)

    for part, plines in by_part.items():
        folder = os.path.join(VO_ROOT, part)
        existing = [f for f in os.listdir(folder) if f.lower().endswith(".mp3")] if os.path.isdir(folder) else []
        ids = sorted({l.id for l in plines}, key=len, reverse=True)

        claimed = {}
        for f in existing:
            name = f[:-4]
            variant = ""
            for v in VARIANTS:
                if name.endswith("_" + v):
                    variant, name = v, name[:-2]
                    break
            for i in ids:
                if name == i or name.startswith(i + "_"):
                    claimed.setdefault((i, variant), f)
                    break

        for l in plines:
            f = claimed.get((l.id, l.variant))
            if f is None and l.variant == "A" and (l.id, "") in claimed:
                f = claimed[(l.id, "")]
            l.file = f or (l.key + ".mp3")


# -- Ledger and fingerprints ------------------------------------------------------------------

def load_json(path, default):
    if not os.path.exists(path):
        return default
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)


def save_json(path, data):
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(data, fh, indent=2, ensure_ascii=False, sort_keys=True)


def tags_supported(cfg):
    return "v3" in cfg.get("model_id", "")


def tag_for(line, delivery):
    """The most specific entry wins: E2_Before_A, then E2_Before, then "*". "" means none."""
    for k in (line.key, line.id, "*"):
        if k in delivery:
            return delivery[k].strip()
    return ""


def spoken(line, cfg, delivery):
    """The text as sent: the delivery tag, then the script's words with the pronunciation fixes."""
    text = line.text if isinstance(line, Line) else line

    for word, say in cfg.get("say_as", {}).items():
        text = text.replace(word, say)

    if isinstance(line, Line) and tags_supported(cfg):
        tag = tag_for(line, delivery)
        if tag:
            text = tag + " " + text

    return text


def load_delivery(cfg):
    delivery = load_json(DELIVERY, {})
    if delivery and not tags_supported(cfg):
        print("Note: delivery.json has tags, but %s does not understand them, so they are left off. "
              "Set model_id to eleven_v3 to use them.\n" % cfg.get("model_id"))
    return delivery


def fingerprint(text, cfg):
    payload = {
        "text": text,
        "voice_id": cfg["voice_id"],
        "model_id": cfg["model_id"],
        "voice_settings": cfg.get("voice_settings", {}),
        "output_format": cfg["output_format"],
        "seed": cfg.get("seed"),
    }
    return hashlib.sha256(json.dumps(payload, sort_keys=True).encode("utf-8")).hexdigest()[:16]


def rel(l):
    return l.part + "/" + l.file


def pending(lines, cfg, ledger, delivery):
    out = []
    for l in lines:
        entry = ledger.get(rel(l))
        if entry and entry.get("fingerprint") == fingerprint(spoken(l, cfg, delivery), cfg):
            continue
        out.append(l)
    return out


# -- ElevenLabs -------------------------------------------------------------------------------

def api_key():
    key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not key:
        sys.exit("Set the ELEVENLABS_API_KEY environment variable first.")
    return key


def request(method, path, body=None, query=""):
    req = urllib.request.Request(API + path + query, method=method,
                                 data=json.dumps(body).encode("utf-8") if body is not None else None,
                                 headers={"xi-api-key": api_key(), "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            return resp.read()
    except urllib.error.HTTPError as e:
        sys.exit("ElevenLabs said %d: %s" % (e.code, e.read().decode("utf-8", "replace")[:500]))


def remaining_characters():
    if not os.environ.get("ELEVENLABS_API_KEY"):
        return None
    sub = json.loads(request("GET", "/user/subscription"))
    return sub.get("character_limit", 0) - sub.get("character_count", 0)


def synthesize(text, cfg):
    body = {"text": text, "model_id": cfg["model_id"], "voice_settings": cfg.get("voice_settings", {})}
    if cfg.get("seed") is not None:
        body["seed"] = cfg["seed"]
    return request("POST", "/text-to-speech/" + cfg["voice_id"], body, "?output_format=" + cfg["output_format"])


# -- Commands ---------------------------------------------------------------------------------

def load_config(need_voice=True):
    cfg = load_json(CONFIG, None)
    if cfg is None:
        sys.exit("No config.json. Copy config.example.json to config.json and fill in voice_id.")
    if need_voice and not cfg.get("voice_id"):
        sys.exit("config.json has no voice_id. Run 'voices' to see yours.")
    return cfg


def select(args):
    cfg = load_config()
    script = os.path.join(VO_ROOT, cfg.get("script", "VO_Script_v7.md"))
    lines = parse_script(script)
    assign_files(lines)

    if args.part:
        lines = [l for l in lines if l.part.lower() == args.part.lower()]
    if args.only:
        wanted = {s.strip() for s in args.only.split(",")}
        lines = [l for l in lines if l.id in wanted or l.key in wanted]

    ledger = load_json(LEDGER, {})
    delivery = load_delivery(cfg)
    return cfg, lines, ledger, delivery, pending(lines, cfg, ledger, delivery)


def cmd_plan(args):
    cfg, lines, ledger, delivery, todo = select(args)

    print("%d lines in scope, %d already generated with these exact settings.\n" % (len(lines), len(lines) - len(todo)))

    total = 0
    for l in todo:
        n = len(spoken(l, cfg, delivery))
        total += n
        new = "" if os.path.exists(os.path.join(VO_ROOT, l.part, l.file)) else "  (new file)"
        tag = tag_for(l, delivery) if tags_supported(cfg) else ""
        print("  %-12s %-36s %4d chars  %s%s" % (l.key, l.part + "/" + l.file, n, tag, new))

    print("\nTo generate: %d lines, %d characters." % (len(todo), total))

    left = remaining_characters()
    if left is not None:
        print("Your account has %d characters left this period." % left)
        if total > left:
            print("NOT ENOUGH. Narrow it with --only or --part.")

    if todo:
        print("\nNothing has been sent. Run the same command as 'generate --go' to spend it.")
    return todo, total, delivery


def cmd_generate(args):
    todo, total, delivery = cmd_plan(args)
    if not todo:
        return
    if not args.go:
        print("\n(dry run - add --go)")
        return

    cfg = load_config()
    ledger = load_json(LEDGER, {})

    for l in todo:
        text = spoken(l, cfg, delivery)
        print("Generating %s ..." % l.key, end=" ", flush=True)

        audio = synthesize(text, cfg)

        dest = os.path.join(STAGING, l.part, l.file)
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        with open(dest, "wb") as fh:
            fh.write(audio)

        # Saved after every line, so an interruption half way loses nothing already paid for.
        ledger[rel(l)] = {
            "fingerprint": fingerprint(text, cfg),
            "line": l.key,
            "text": l.text,
            "sent": text,
            "characters": len(text),
            "generated": datetime.now().isoformat(timespec="seconds"),
            "installed": False,
        }
        save_json(LEDGER, ledger)
        print("ok")

    print("\nDone. Listen in %s, then run 'install'." % STAGING)


def cmd_install(args):
    ledger = load_json(LEDGER, {})
    count = 0

    for part in ("Tutorial", "Journey"):
        src_dir = os.path.join(STAGING, part)
        if not os.path.isdir(src_dir):
            continue
        for f in sorted(os.listdir(src_dir)):
            if args.only and not any(f.startswith(o.strip()) for o in args.only.split(",")):
                continue
            dst = os.path.join(VO_ROOT, part, f)
            shutil.copyfile(os.path.join(src_dir, f), dst)
            entry = ledger.get(part + "/" + f)
            if entry:
                entry["installed"] = True
            print("  %s/%s%s" % (part, f, "" if os.path.exists(dst + ".meta") else "  (new: Unity will import it)"))
            count += 1

    save_json(LEDGER, ledger)
    print("\nInstalled %d file(s). In Unity: VO Timeline > Scan Recordings, then Report." % count)


def cmd_voices(args):
    data = json.loads(request("GET", "/voices"))
    for v in data.get("voices", []):
        print("%-24s %s  (%s)" % (v.get("voice_id"), v.get("name"), v.get("category", "")))


def cmd_test(args):
    cfg = load_config()
    text = args.text

    # --as A1: the text with that line's delivery tag, as generate would send it.
    if args.as_line:
        delivery = load_delivery(cfg)
        fake = Line("Tutorial", args.as_line, "", text, "narration")
        text = spoken(fake, cfg, delivery)
    else:
        text = spoken(text, cfg, {})

    print("Sending %d characters: %s" % (len(text), text))
    print("Sending %d characters." % len(text))
    audio = synthesize(text, cfg)
    os.makedirs(STAGING, exist_ok=True)
    out = os.path.join(STAGING, "_test.mp3")
    with open(out, "wb") as fh:
        fh.write(audio)
    print("Wrote " + out)


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd")

    for name in ("plan", "generate"):
        s = sub.add_parser(name)
        s.add_argument("--only", help="comma-separated line ids, e.g. D5,N_A1,E2_Before_A")
        s.add_argument("--part", choices=["tutorial", "journey"])
        if name == "generate":
            s.add_argument("--go", action="store_true", help="actually send it (costs characters)")

    s = sub.add_parser("install")
    s.add_argument("--only", help="comma-separated file name prefixes")

    sub.add_parser("voices")

    s = sub.add_parser("test")
    s.add_argument("text", help="what to say; tags like [softly] can be typed in directly")
    s.add_argument("--as", dest="as_line", help="also apply this line id's tag from delivery.json")

    args = p.parse_args()
    {"plan": cmd_plan, "generate": cmd_generate, "install": cmd_install,
     "voices": cmd_voices, "test": cmd_test}.get(args.cmd, lambda a: p.print_help())(args)


if __name__ == "__main__":
    main()

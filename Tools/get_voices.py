"""Downloads the offline neural voice pack (Piper + German voices) into ./voices (not tracked by git).

    python Tools/get_voices.py

Sources:
  Piper (MIT)   https://github.com/rhasspy/piper/releases/tag/2023.11.14-2   piper_windows_amd64.zip
  Voices        https://huggingface.co/rhasspy/piper-voices
    de_DE-thorsten_emotional-medium  CC0       (Floki, 8 emotional speakers)
    de_DE-mls-medium                 CC-BY 4.0 (236 speakers for everybody else; credit "MLS / OpenSLR 94")
Existing files are skipped, so the script can be re-run safely.
"""
import os
import sys
import urllib.request
import zipfile

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "voices")
HF = "https://huggingface.co/rhasspy/piper-voices/resolve/main/de/de_DE"

PIPER_ZIP = ("https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip", "piper_windows_amd64.zip")
MODELS = [
    (f"{HF}/thorsten_emotional/medium/de_DE-thorsten_emotional-medium.onnx", "de_DE-thorsten_emotional-medium.onnx"),
    (f"{HF}/thorsten_emotional/medium/de_DE-thorsten_emotional-medium.onnx.json", "de_DE-thorsten_emotional-medium.onnx.json"),
    (f"{HF}/mls/medium/de_DE-mls-medium.onnx", "de_DE-mls-medium.onnx"),
    (f"{HF}/mls/medium/de_DE-mls-medium.onnx.json", "de_DE-mls-medium.onnx.json"),
]


def download(url, target):
    if os.path.exists(target) and os.path.getsize(target) > 0:
        print("skip (exists):", os.path.basename(target))
        return
    os.makedirs(os.path.dirname(target), exist_ok=True)
    tmp = target + ".part"
    print("downloading", os.path.basename(target))
    with urllib.request.urlopen(url) as response, open(tmp, "wb") as out:
        total = int(response.headers.get("Content-Length", 0))
        done = 0
        while True:
            chunk = response.read(1 << 20)
            if not chunk:
                break
            out.write(chunk)
            done += len(chunk)
            if total:
                print(f"\r  {done / 1e6:6.1f} / {total / 1e6:.1f} MB", end="", flush=True)
    print()
    os.replace(tmp, target)


def main():
    url, name = PIPER_ZIP
    zip_path = os.path.join(ROOT, name)
    if not os.path.exists(os.path.join(ROOT, "piper", "piper.exe")):
        download(url, zip_path)
        with zipfile.ZipFile(zip_path) as archive:
            archive.extractall(ROOT)
        os.remove(zip_path)
        print("extracted piper to", os.path.join(ROOT, "piper"))
    else:
        print("skip (exists): piper")

    for url, name in MODELS:
        download(url, os.path.join(ROOT, "models", name))

    print("done.")


if __name__ == "__main__":
    sys.exit(main())

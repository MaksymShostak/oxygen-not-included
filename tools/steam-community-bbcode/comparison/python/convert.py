# SPDX-License-Identifier: AGPL-3.0-only
"""Invoke unmodified, documented comparator APIs using UTF-8 stdin/stdout."""
import sys

sys.stdin.reconfigure(encoding="utf-8", newline="")
sys.stdout.reconfigure(encoding="utf-8", newline="")
source = sys.stdin.read()
match sys.argv[1]:
    case "steamify-forward":
        from steamify import to_markdown

        output = to_markdown(source)
    case "steamify-reverse":
        from steamify import to_steam

        output = to_steam(source)
    case "steam-editor-tools-reverse":
        from steam_editor_tools import BBCodeRenderer, DocumentParser

        output = BBCodeRenderer().render(DocumentParser().parse_markdown(source))
    case _:
        raise ValueError("Unknown comparison API")

if not isinstance(output, str):
    raise TypeError("The documented conversion API returned a non-string result")
sys.stdout.write(output)

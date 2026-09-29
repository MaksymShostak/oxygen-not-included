import os
import subprocess
import sys

EXIT_FAILURE = 1
EXIT_INTERRUPTED = 130
ZIP_PATH = os.path.join("artifacts", "release-source.zip")

def print_yellow(text):
    print(f"\033[93m{text}\033[0m")

def print_cyan(text):
    print(f"\033[96m{text}\033[0m")

def print_green(text):
    print(f"\033[92m{text}\033[0m")

def run_command(cmd, capture_output=False):
    try:
        result = subprocess.run(cmd, text=True, check=True, capture_output=capture_output)
        return result
    except (subprocess.CalledProcessError, OSError) as e:
        print(f"Error executing command: {e}")
        return None

def require_command(cmd, capture_output=False):
    result = run_command(cmd, capture_output=capture_output)
    if result is None:
        print(f"Aborting: {' '.join(cmd)} failed.")
        sys.exit(EXIT_FAILURE)
    return result

def prompt(message):
    try:
        return input(message)
    except KeyboardInterrupt:
        print("\nCancelled.")
        sys.exit(EXIT_INTERRUPTED)
    except EOFError:
        print("\nCancelled: no input available.")
        sys.exit(EXIT_FAILURE)

def main():
    # Enable ANSI escape sequences on Windows
    if sys.platform == "win32":
        os.system("")

    # Anchor every Git operation and the exported archive to the repository root
    repo_root = require_command(["git", "rev-parse", "--show-toplevel"], capture_output=True).stdout.strip()
    os.chdir(repo_root)

    print_yellow("Remember to update version!")
    print_yellow("Make sure the built binary does not contain temporary code!\n")

    print_cyan("Git status:")
    require_command(["git", "status", "--porcelain"])

    prompt("\nPress Enter to continue clean, or Ctrl+C to cancel...")

    # Unstage everything so staged additions are offered to the interactive clean
    print_cyan("\nUnstaging changes...")
    require_command(["git", "reset", "--quiet", "HEAD"])

    # Clean untracked files interactively, keeping the active virtual environment
    print_cyan("\nCleaning untracked files...")
    require_command(["git", "clean", "-idx", "-e", ".venv/", "."])

    # Revert changes to tracked files
    print_cyan("\nResetting tracked files...")
    require_command(["git", "checkout", "--", "."])

    # Ask to export source zip
    zip_confirm = prompt("\nWould you like to export a clean source ZIP for release? (y/N): ").strip().lower()

    if zip_confirm in ("y", "yes"):
        print_cyan(f"Exporting repository to {ZIP_PATH}...")
        os.makedirs(os.path.dirname(ZIP_PATH), exist_ok=True)
        require_command(["git", "archive", "--format=zip", "HEAD", "-o", ZIP_PATH])
        print_green(f"Export complete! Saved to: {ZIP_PATH}")

if __name__ == "__main__":
    main()

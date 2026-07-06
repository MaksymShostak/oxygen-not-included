import subprocess
import sys

def print_yellow(text):
    print(f"\033[93m{text}\033[0m")

def print_cyan(text):
    print(f"\033[96m{text}\033[0m")

def print_green(text):
    print(f"\033[92m{text}\033[0m")

def run_command(cmd):
    try:
        result = subprocess.run(cmd, text=True, check=True)
        return result
    except subprocess.CalledProcessError as e:
        print(f"Error executing command: {e}")
        return None

def main():
    # Enable ANSI escape sequences on Windows
    if sys.platform == "win32":
        import os
        os.system("")

    print_yellow("Remember to update version!")
    print_yellow("Make sure the built binary does not contain temporary code!\n")

    print_cyan("Git status:")
    run_command(["git", "status", "--porcelain"])

    try:
        input("\nPress Enter to continue clean, or Ctrl+C to cancel...")
    except (KeyboardInterrupt, EOFError):
        print("\nCancelled.")
        sys.exit(0)

    # Clean untracked files interactively
    print_cyan("\nCleaning untracked files...")
    subprocess.run(["git", "clean", "-idx", "."])

    # Revert changes to tracked files
    print_cyan("\nResetting tracked files...")
    run_command(["git", "checkout", "."])

    # Ask to export source zip
    try:
        zip_confirm = input("\nWould you like to export a clean source ZIP for release? (y/N): ").strip().lower()
    except (KeyboardInterrupt, EOFError):
        print("\nCancelled.")
        sys.exit(0)

    if zip_confirm in ("y", "yes"):
        zip_path = "release-source.zip"
        print_cyan(f"Exporting repository to {zip_path}...")
        result = run_command(["git", "archive", "--format=zip", "HEAD", "-o", zip_path])
        if result is not None:
            print_green(f"Export complete! Saved to: {zip_path}")

if __name__ == "__main__":
    main()

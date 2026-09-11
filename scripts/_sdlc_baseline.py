"""Contracts for previously accepted plan text alongside actual Issue snapshots.

Plan content is opaque UTF-8. Native Git/GitHub blob comparison owns its identity;
this module does not parse Markdown or attest owner acceptance.
"""
from pathlib import PurePosixPath
from _commands import SetupError


def is_canonical_plan_path(path: str) -> bool:
    """Recognize a canonical repository-relative plan path, without traversal."""
    supplied = PurePosixPath(path)
    return (supplied.as_posix() == path and not supplied.is_absolute()
            and '\\' not in path and '..' not in supplied.parts
            and supplied.parts[:2] == ('docs', 'plans')
            and len(supplied.parts) >= 3 and supplied.suffix == '.md')


def require_plan_text(content: object) -> str:
    """Require actual nonempty plan text; validity of its intent is a review duty."""
    if not isinstance(content, str) or not content.strip() or '\x00' in content:
        raise SetupError('A plan baseline must contain nonempty UTF-8 text without NUL.')
    return content


def require_acceptance_reference(reference: str) -> None:
    """Reject placeholders without treating a supplied reference as approval."""
    if reference.strip().lower() in {'', 'none', 'n/a', '-', 'pending'}:
        raise SetupError('A committed plan requires an inspectable owner acceptance reference.')

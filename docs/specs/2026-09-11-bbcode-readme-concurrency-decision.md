# README concurrency decision

The owner approved optimistic concurrency in the migration conversation on
11 September 2026 after independent review identified the final comparison/replace
race. This decision narrows AC-004 in the accepted BBCode README consumer spec.

Synchronization rechecks the target path and exact README bytes after conversion
and immediately before atomic replacement. A detected edit rejects the write and
preserves the editor's content. Temporary output is separate from the original;
conversion and output-generation failures do not truncate the README.

This is not a compare-and-swap filesystem transaction. An unrelated editor can save
between the final comparison and replacement, and that edit can be overwritten.
Do not edit the README concurrently with synchronization. Stronger guarantees would
require a different write design and coordination by every writer.

All other AC-004 failure cases and acceptance criteria remain unchanged. This
decision does not waive installed-consumer, release, registry, or review evidence.

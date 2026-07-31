#!/usr/bin/env python3
from pathlib import Path

path = Path("ErsatzTV/Pages/ScheduleItemsEditor.razor")
text = path.read_text(encoding="utf-8-sig")
old = "private readonly HashSet<ProgramScheduleItemEditViewModel> _bulkSelectedItems = [];"
new = "private readonly System.Collections.Generic.HashSet<ProgramScheduleItemEditViewModel> _bulkSelectedItems = [];"
count = text.count(old)
if count != 1:
    raise RuntimeError(f"Expected one HashSet declaration, found {count}")
path.write_text(text.replace(old, new), encoding="utf-8")
print("Qualified schedule bulk selection HashSet")

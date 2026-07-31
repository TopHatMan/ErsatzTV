#!/usr/bin/env python3
from pathlib import Path

path = Path("ErsatzTV/Pages/ScheduleItemsEditor.razor")
text = path.read_text(encoding="utf-8-sig")
old = "Rotate Start, Play in Order"
new = "Random Start, Play in Order"
count = text.count(old)
if count != 3:
    raise RuntimeError(f"Expected 3 UI labels, found {count}")
path.write_text(text.replace(old, new), encoding="utf-8")
print("Renamed playback order label")

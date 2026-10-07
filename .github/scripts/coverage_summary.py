import sys
import xml.etree.ElementTree as ElementTree
from pathlib import Path

SOURCE_ROOT = "src/FixFlow.Mobile.Core/"
FEATURES = ("Auth", "WorkOrders", "ServiceEntries", "Parts", "Sync", "Settings")


def relative_source_path(filename):
    normalized = filename.replace("\\", "/")
    index = normalized.find(SOURCE_ROOT)
    return normalized[index + len(SOURCE_ROOT):] if index >= 0 else normalized


def collect_line_hits(report_directory):
    covered_by_line = {}
    for report in sorted(Path(report_directory).glob("**/*.cobertura.xml")):
        for class_element in ElementTree.parse(report).getroot().iter("class"):
            source_path = relative_source_path(class_element.get("filename", ""))
            for line in class_element.findall("lines/line"):
                key = (source_path, int(line.get("number")))
                covered_by_line[key] = covered_by_line.get(key, False) or int(line.get("hits")) > 0
    return covered_by_line


def area_of(source_path):
    parts = source_path.split("/")
    if len(parts) > 2 and parts[0] == "Features" and parts[1] in FEATURES:
        return parts[1]
    return "Other"


def format_row(name, covered, total):
    percentage = 100 * covered / total if total else 0
    return f"| {name} | {covered} | {total} | {percentage:.1f}% |"


def main():
    covered_by_line = collect_line_hits(sys.argv[1])
    if not covered_by_line:
        print("No coverage reports found.")
        return 1

    totals = {}
    for (source_path, _), covered in covered_by_line.items():
        area_covered, area_total = totals.get(area_of(source_path), (0, 0))
        totals[area_of(source_path)] = (area_covered + int(covered), area_total + 1)

    print("## Code coverage of FixFlow.Mobile.Core (lines, unit and integration tests combined)")
    print()
    print("| Area | Covered | Total | Coverage |")
    print("|---|---|---|---|")
    for area in (*FEATURES, "Other"):
        if area in totals:
            print(format_row(area, *totals[area]))
    covered_lines = sum(covered for covered, _ in totals.values())
    total_lines = sum(total for _, total in totals.values())
    print(format_row("**Total**", covered_lines, total_lines))
    return 0


if __name__ == "__main__":
    sys.exit(main())

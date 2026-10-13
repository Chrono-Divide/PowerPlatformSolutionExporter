"""Compare an app export with real Power Apps Web UI ZIP downloads.

Only row order, column order, quoting, BOM and record terminators are ignored.
Column names, column scope, primary IDs and every CSV cell remain exact strings.
Reports contain counts, column names and hashes, never business values or row IDs.
"""
import argparse
import csv
import hashlib
import io
import json
from collections import Counter
from pathlib import Path
from zipfile import ZipFile


def read_csv(raw, primary_id):
    rows = [row for row in csv.reader(io.StringIO(raw.decode('utf-8-sig'), newline=''), strict=True) if row]
    if not rows:
        raise ValueError('Missing CSV header')
    header, data = rows[0], rows[1:]
    if len(set(header)) != len(header) or primary_id not in header:
        raise ValueError('Duplicate header or missing Primary ID')
    if any(len(row) != len(header) for row in data):
        raise ValueError('CSV record width differs from its header')
    primary_index = header.index(primary_id)
    keyed = {row[primary_index]: dict(zip(header, row)) for row in data}
    if '' in keyed or len(keyed) != len(data):
        raise ValueError('Empty or duplicate Primary ID')
    canonical = json.dumps([[row_id, sorted(row.items())] for row_id, row in sorted(keyed.items())], ensure_ascii=False, separators=(',', ':'))
    return header, keyed, hashlib.sha256(canonical.encode('utf-8')).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--export-dir', required=True, type=Path)
    parser.add_argument('--web-ui', required=True, action='append', type=Path)
    parser.add_argument('--report', required=True, type=Path)
    parser.add_argument('--require-all-tables', action='store_true')
    args = parser.parse_args()
    manifest = json.loads((args.export_dir / 'manifest.json').read_text(encoding='utf-8-sig'))
    tables = {table['fileName']: table for table in manifest['tables']}
    comparisons = []
    compared = set()
    for reference in args.web_ui:
        with ZipFile(reference) as archive:
            for entry in archive.namelist():
                if not entry.lower().endswith('.csv'):
                    continue
                filename = Path(entry).name
                if filename not in tables:
                    raise ValueError(f'Web UI file is not in the app manifest: {filename}')
                table = tables[filename]
                app_header, app_rows, app_hash = read_csv((args.export_dir / filename).read_bytes(), table['primaryIdAttribute'])
                web_header, web_rows, web_hash = read_csv(archive.read(entry), table['primaryIdAttribute'])
                common_columns = sorted(set(app_header) & set(web_header))
                mismatches = Counter()
                for row_id in set(app_rows) & set(web_rows):
                    for column in common_columns:
                        if app_rows[row_id][column] != web_rows[row_id][column]:
                            mismatches[column] += 1
                comparison = {
                    'table': table['logicalName'], 'fileName': filename,
                    'referenceZip': reference.name,
                    'referenceZipSha256': hashlib.sha256(reference.read_bytes()).hexdigest(),
                    'appRows': len(app_rows), 'webUiRows': len(web_rows),
                    'appColumns': len(app_header), 'webUiColumns': len(web_header),
                    'appOnlyColumns': sorted(set(app_header) - set(web_header)),
                    'webUiOnlyColumns': sorted(set(web_header) - set(app_header)),
                    'appOnlyPrimaryIds': len(set(app_rows) - set(web_rows)),
                    'webUiOnlyPrimaryIds': len(set(web_rows) - set(app_rows)),
                    'rawCellMismatchCount': sum(mismatches.values()),
                    'rawCellsCompared': len(set(app_rows) & set(web_rows)) * len(common_columns),
                    'rawCellMismatchesByColumn': dict(mismatches),
                    'appCanonicalSha256': app_hash, 'webUiCanonicalSha256': web_hash,
                    'passed': set(app_header) == set(web_header) and app_hash == web_hash,
                }
                comparisons.append(comparison)
                compared.add(filename)
                print(f"{'PASS' if comparison['passed'] else 'FAIL'} {filename}: {len(web_rows)} rows, {len(web_header)} columns, {sum(mismatches.values())} raw cell mismatches")
    report = {
        'sourceEnvironmentName': manifest['sourceEnvironmentName'],
        'sourceEnvironmentUrl': manifest['sourceEnvironmentUrl'],
        'solutionVersion': manifest['solutionVersion'],
        'exportTableCount': len(tables), 'exportRecordCount': manifest['recordCount'],
        'verifiedTableCount': len(compared),
        'rawCellsCompared': sum(item['rawCellsCompared'] for item in comparisons),
        'comparison': 'Exact column names/scope and exact CSV cell strings, keyed by Primary ID. Row/column order and CSV serialization are ignored.',
        'passed': bool(comparisons) and all(item['passed'] for item in comparisons) and (not args.require_all_tables or set(tables) == compared),
        'tablesWithoutWebUiReference': sorted(set(tables) - compared),
        'tables': comparisons,
    }
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f"Verified {len(compared)}/{len(tables)} tables. Report: {args.report}")
    return 0 if report['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())

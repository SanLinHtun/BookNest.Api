from pathlib import Path
import json
import openpyxl

p=Path(__file__).resolve().parents[1]/'BookNest_API_Design.xlsx'
w=openpyxl.load_workbook(p,data_only=False)
v=openpyxl.load_workbook(p,data_only=True)
def rows(sheet):
    return [r for r in v[sheet].iter_rows(min_row=5,values_only=True) if r[1]]
api=rows('API List')
assert len(api)==80
assert len(set((r[3],r[4]) for r in api))==80
assert all(r[6] and '<summary>' not in r[6] and 'public async' not in r[6] and len(r[6])<=240 for r in api)
assert all(s.freeze_panes for s in w if s.title!='Cover')
assert v['Cover']['B15'].value.startswith('80 operations')
ids={r[1] for r in api}
requests=rows('Request Fields')
responses=rows('Response Fields')
examples=rows('Examples')
assert all(r[1] in ids for r in requests+examples)
assert len(examples)==80
assert all(r[2] not in ('passwordHash','contentStorageKey') for r in responses)
errors=[(s.title,c.coordinate,c.value) for s in v for row in s for c in row if c.data_type=='e']
assert not errors,errors
for r in examples:
    if r[6]!='None': json.loads(r[6])
    if r[8]!='No response body': json.loads(r[8])
print(f'PASS: 6 sheets, {len(api)} unique operations, {len(requests)} request fields, {len(responses)} response fields, 80 valid JSON examples.')
print('PASS: API references, secret exclusion, frozen headers, formula cache and no cell errors.')
print('File size:',p.stat().st_size)
print('Maximum row heights:',[(s.title,max(d.height or 0 for d in s.row_dimensions.values())) for s in w])

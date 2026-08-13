// A permanent, cheap type-level guard, checked by `npm run typecheck`
// (and therefore by tsc -b at the workspace root) -- proves the generated
// schema (src/schema.ts) reflects real request shapes, not just that
// regeneration produced syntactically valid TypeScript. Cheap insurance
// against the exact class of doc-drift bug this codebase has hit
// repeatedly (docs/ai/README.md's own trap list): a schema regenerated
// from a stale docs/openapi/v1.json would silently pass `tsc` even though
// it no longer matches the real API. Add a line here whenever a request
// field that matters gets added.
import type { operations, components } from './schema';

// IDN-11 -- the manager-authorisation fields landed on the real request type.
const voidRequest: components['schemas']['VoidLineRequest'] = {
  reason: 'test',
  managerStaffId: '019fe4bf-591b-7889-a3c5-887facb80218',
  managerPin: '1234',
};

// FLR-06 -- the section-assignment request type exists and is nullable.
const sectionRequest: components['schemas']['AssignRoomSectionRequest'] = {
  staffId: null,
};

// @ts-expect-error -- a field that was never real should not typecheck.
const invalid: components['schemas']['VoidLineRequest'] = { reason: 'x', notAField: true };

// Response bodies (the gap this package's own README documents closing for
// success responses -- Brasa.Api's endpoints now carry .Produces<T>()
// metadata) -- a 200's content is a real schema, not `never`. If a future
// endpoint change forgets .Produces<T>() again, this line stops
// typechecking rather than silently reverting to an untyped response.
type FloorResponseBody = operations['GetFloor']['responses'][200]['content']['application/json'];
const floorResponse: FloorResponseBody = [
  { id: 'x', name: 'Salão', displayOrder: 0, floorLevel: 0, assignedStaffId: null, assignedStaffName: null, tables: [] },
];

void voidRequest;
void sectionRequest;
void invalid;
void floorResponse;

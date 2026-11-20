# Validation: Service catalog and API governance

Validation performed after all waves complete.

## Method

- `dotnet build` — solution builds with no warnings as errors.
- `dotnet test` — all test projects pass.
- `curl http://localhost:<port>/swagger/v1/swagger.json` — each HTTP service
  returns OpenAPI JSON.
- Service discovery registration verified via Consul agent API.

## Evidence

- Build output: (to be recorded)
- Test output: (to be recorded)
- OpenAPI spec retrieval: (to be recorded)
- Discovery registration: (to be recorded)

## Result

PENDING — validation not yet performed.

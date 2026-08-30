# @squad-crm/shared-ui

Shared PrimeNG presentation setup: the single place the PrimeNG theme and
configuration are defined for every Squad CRM surface.

Platform-neutral by design — it must not depend on `@squad-crm/platform` or on
any application. PrimeNG wrappers remain exceptional: `sc-language-switcher`
exists because its repeated accessibility and locale-selection behavior is
shared, but it accepts plain inputs/outputs and knows nothing about application
state. Applications bridge it to their locale service at the composition root.

The library also owns common UI resources and the platform-neutral
`PrimeNgLocaleAdapter`; applications supply the active locale. See [the
workspace README](../../README.md) for boundaries and usage.

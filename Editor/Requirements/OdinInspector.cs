// Odin Inspector comes from the Asset Store, so package.json cannot pull it in.
// Without it the assemblies that use Odin are left out, and this one line says why.
#if !ODIN_INSPECTOR
#error RiseOn.Outline2D needs Odin Inspector. Install it from the Asset Store, or add the ODIN_INSPECTOR scripting define for the current platform.
#endif

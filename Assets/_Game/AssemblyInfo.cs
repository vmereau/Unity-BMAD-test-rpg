using System.Runtime.CompilerServices;

// Lets EditMode tests reach internal test hooks (e.g. SaveFileStore.FolderOverride).
[assembly: InternalsVisibleTo("Tests.EditMode")]

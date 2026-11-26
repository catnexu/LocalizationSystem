using UnityEditor;
using UnityEngine.UIElements;

namespace LocalizationSystem.Editor
{
    /// <summary>
    /// A block of <see cref="LocalizationSettings"/> inspector. Implementations are discovered via TypeCache,
    /// so optional modules (e.g. Google Sheets) can add their own settings UI.
    /// </summary>
    internal abstract class LocalizationSettingsSection
    {
        public abstract string Title { get; }
        public virtual int Order => 0;
        public abstract VisualElement CreateGUI(SerializedObject settings);
    }
}

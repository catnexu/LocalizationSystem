using System;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace LocalizationSystem.Editor
{
    [CustomEditor(typeof(LocalizationSettings))]
    internal sealed class LocalizationSettingsEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            var sections = TypeCache.GetTypesDerivedFrom<LocalizationSettingsSection>()
                .Where(t => !t.IsAbstract)
                .Select(t => (LocalizationSettingsSection) Activator.CreateInstance(t))
                .OrderBy(s => s.Order);

            foreach (LocalizationSettingsSection section in sections)
            {
                Foldout foldout = new Foldout {text = section.Title, value = true, style = {marginBottom = 10}};
                foldout.Add(section.CreateGUI(serializedObject));
                root.Add(foldout);
            }

            return root;
        }
    }
}

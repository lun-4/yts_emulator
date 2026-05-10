#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System;

namespace DMNware.AppGen
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class DmnwareAppAttribute : Attribute
    {
        public string Name { get; }
        public string IconKey { get; }
        public string SplashKey { get; }
        public bool HandheldOnly { get; }
        public bool VrOnly { get; }
        public bool HiddenFromUser { get; }

        public DmnwareAppAttribute(
            string name,
            string iconKey = null,
            string splashKey = null,
            bool handheldOnly = false,
            bool vrOnly = false,
            bool hiddenFromUser = false)
        {
            Name = name;
            IconKey = iconKey;
            SplashKey = splashKey;
            HandheldOnly = handheldOnly;
            VrOnly = vrOnly;
            HiddenFromUser = hiddenFromUser;
        }
    }
}
#endif

using System;


namespace DependinatorVse
{
    /// <summary>
    ///     Helper class that exposes all GUIDs used across VS Package.
    /// </summary>
    internal sealed class PackageGuids
    {
        public const string guidShowInDependinatorCommandPackageString = "a85ebc56-4887-4217-a3d5-86cd3c5062da";
        public const string guidShowInDependinatorCommandPackageCmdSetString = "3503f324-db31-4488-9db2-c484cbd1609c";
        public const string guidImagesString = "3ca0e818-1182-47f9-b977-42ad9670c65f";
        public static Guid guidShowInDependinatorCommandPackage = new Guid(guidShowInDependinatorCommandPackageString);

        public static Guid guidShowInDependinatorCommandPackageCmdSet =
            new Guid(guidShowInDependinatorCommandPackageCmdSetString);

        public static Guid guidImages = new Guid(guidImagesString);
    }


    /// <summary>
    ///     Helper class that encapsulates all CommandIDs uses across VS Package.
    /// </summary>
    internal sealed class PackageIds
    {
        public const int MyMenuGroup = 0x1020;
        public const int ShowInDependinatorCommandId = 0x0100;
        public const int bmpPic1 = 0x0001;
    }
}

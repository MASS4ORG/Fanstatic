namespace Fanstatic.Models;

public static partial class FanstaticExt
{
    public static DateTime BuildDate => _BuildDate();
    public static long BuildDateTicks() => _BuildDateTicks();

    private static partial DateTime _BuildDate();
    private static partial long _BuildDateTicks();
}

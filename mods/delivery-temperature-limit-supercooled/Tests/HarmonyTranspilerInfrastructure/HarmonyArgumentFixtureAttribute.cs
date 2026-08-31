namespace HarmonyLib;

[AttributeUsage(
    AttributeTargets.Parameter |
    AttributeTargets.Method |
    AttributeTargets.Class |
    AttributeTargets.Struct,
    AllowMultiple = true)]
public sealed class HarmonyArgument : Attribute
{
    public HarmonyArgument(string originalName)
        : this(originalName, null)
    {
    }

    public HarmonyArgument(int index)
        : this(index, null)
    {
    }

    public HarmonyArgument(string originalName, string? newName)
    {
        OriginalName = originalName;
        Index = -1;
        NewName = newName;
    }

    public HarmonyArgument(int index, string? newName)
    {
        Index = index;
        NewName = newName;
    }

    public string? OriginalName { get; }

    public int Index { get; }

    public string? NewName { get; }
}

public sealed class CodeInstruction
{
}

public delegate ref T RefResult<T>();

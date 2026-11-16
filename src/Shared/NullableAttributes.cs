// Internal copies of the nullable static-analysis attributes, for .NET Framework 3.5, which lacks them.
// The compiler recognises these attributes by their full name, so these copies give the net35 builds the
// same nullable analysis as the net10.0 builds. Directory.Build.targets compiles this file into every net35
// project, so each assembly gets its own internal copy. Never make one assembly's copies visible to another
// net35 assembly (InternalsVisibleTo): the duplicates would conflict (CS0436).
// The shapes match the public .NET API: https://learn.microsoft.com/dotnet/api/system.diagnostics.codeanalysis

namespace System.Diagnostics.CodeAnalysis;

/// <summary>Null is allowed as an input, even if the type disallows it.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property, Inherited = false)]
internal sealed class AllowNullAttribute : Attribute
{
}

/// <summary>Null is disallowed as an input, even if the type allows it.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property, Inherited = false)]
internal sealed class DisallowNullAttribute : Attribute
{
}

/// <summary>An output may be null, even if the type disallows it.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, Inherited = false)]
internal sealed class MaybeNullAttribute : Attribute
{
}

/// <summary>An output is not null, even if the type allows it.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, Inherited = false)]
internal sealed class NotNullAttribute : Attribute
{
}

/// <summary>The parameter may be null when the method returns <see cref="ReturnValue"/>.</summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
internal sealed class MaybeNullWhenAttribute : Attribute
{
    public MaybeNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

    public bool ReturnValue { get; }
}

/// <summary>The parameter is not null when the method returns <see cref="ReturnValue"/>.</summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
internal sealed class NotNullWhenAttribute : Attribute
{
    public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

    public bool ReturnValue { get; }
}

/// <summary>The output is not null if the named parameter is not null.</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, AllowMultiple = true, Inherited = false)]
internal sealed class NotNullIfNotNullAttribute : Attribute
{
    public NotNullIfNotNullAttribute(string parameterName) => ParameterName = parameterName;

    public string ParameterName { get; }
}

/// <summary>The method never returns normally.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class DoesNotReturnAttribute : Attribute
{
}

/// <summary>The method doesn't return when the parameter has the value <see cref="ParameterValue"/>.</summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
internal sealed class DoesNotReturnIfAttribute : Attribute
{
    public DoesNotReturnIfAttribute(bool parameterValue) => ParameterValue = parameterValue;

    public bool ParameterValue { get; }
}

/// <summary>The listed members are not null when the method or property returns.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
internal sealed class MemberNotNullAttribute : Attribute
{
    public MemberNotNullAttribute(string member) => Members = [member];

    public MemberNotNullAttribute(params string[] members) => Members = members;

    public string[] Members { get; }
}

/// <summary>The listed members are not null when the method or property returns <see cref="ReturnValue"/>.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
internal sealed class MemberNotNullWhenAttribute : Attribute
{
    public MemberNotNullWhenAttribute(bool returnValue, string member)
    {
        ReturnValue = returnValue;
        Members = [member];
    }

    public MemberNotNullWhenAttribute(bool returnValue, params string[] members)
    {
        ReturnValue = returnValue;
        Members = members;
    }

    public bool ReturnValue { get; }

    public string[] Members { get; }
}

using System;
using System.Collections.Generic;

namespace CsToBeamNG.Converter.Networks;

/// <summary>How summaries such as <c>cstobeamng inspect</c>'s name the <see cref="NetCategory"/>s, and in which order.</summary>
public static class NetCategoryNames
{
    /// <summary>The categories from the most to the least important for driving, <see cref="NetCategory.Other"/> last.</summary>
    public static IReadOnlyList<NetCategory> InSummaryOrder { get; } =
        [NetCategory.Highway, NetCategory.Arterial, NetCategory.Local, NetCategory.Path, NetCategory.Rail, NetCategory.Other];

    /// <summary>Returns a category's name in lowercase words, such as <c>arterial</c>.</summary>
    /// <param name="category">The category.</param>
    public static string Of(NetCategory category) => category switch
    {
        NetCategory.Highway => "highway",
        NetCategory.Arterial => "arterial",
        NetCategory.Local => "local",
        NetCategory.Path => "path",
        NetCategory.Rail => "rail",
        NetCategory.Other => "other",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown network category."),
    };
}

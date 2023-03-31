﻿// <copyright file="SearchTestString.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Models;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using static FhirServerHarness.Search.SearchDefinitions;

namespace FhirServerHarness.Search;

/// <summary>A search test string.</summary>
public static class EvalStringSearch
{
    /// <summary>Tests a string search value against string-type nodes, using starts-with & case-insensitive.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringStartsWith(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string value = (string)(valueNode?.Value ?? string.Empty);

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return sp.Values.Any(v => value.StartsWith(v, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Tests a string search value against string-type nodes, using contains & case-insensitive.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringContains(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string value = (string)(valueNode?.Value ?? string.Empty);

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return sp.Values.Any(v => value.Contains(v, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Tests a string search value against string-type nodes, using exact matching (equality & case-sensitive).</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringExact(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string value = (string)(valueNode?.Value ?? string.Empty);

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return sp.Values.Any(v => value.Equals(v, StringComparison.Ordinal));
    }

    /// <summary>Tests a string search value against a human name (family, given, or text), using starts-with & case-insensitive.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringStartsWithAgainstHumanName(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode == null)
        {
            return false;
        }

        Hl7.Fhir.Model.HumanName hn = valueNode.ToPoco<HumanName>();

        return sp.Values.Any(v => 
            (hn.Family?.StartsWith(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (hn.Given?.Any(gn => gn.StartsWith(v, StringComparison.OrdinalIgnoreCase)) ?? false) ||
            (hn.Text?.StartsWith(v, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    /// <summary>Tests a string search value against a human name (family, given, or text), using contains & case-insensitive.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringContainsAgainstHumanName(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode == null)
        {
            return false;
        }

        Hl7.Fhir.Model.HumanName hn = valueNode.ToPoco<HumanName>();

        return sp.Values.Any(v =>
            (hn.Family?.Contains(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (hn.Given?.Any(gn => gn.Contains(v, StringComparison.OrdinalIgnoreCase)) ?? false) ||
            (hn.Text?.Contains(v, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    /// <summary>Tests a string search value against a human name (family, given, or text), using exact matching (case-sensitive).</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringExactAgainstHumanName(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode == null)
        {
            return false;
        }

        Hl7.Fhir.Model.HumanName hn = valueNode.ToPoco<HumanName>();

        return sp.Values.Any(v =>
            (hn.Family?.Equals(v, StringComparison.Ordinal) ?? false) ||
            (hn.Given?.Any(gn => gn.Equals(v, StringComparison.Ordinal)) ?? false) ||
            (hn.Text?.Equals(v, StringComparison.Ordinal) ?? false));
    }
}

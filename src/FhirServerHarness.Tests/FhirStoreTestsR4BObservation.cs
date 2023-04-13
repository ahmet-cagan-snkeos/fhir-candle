﻿// <copyright file="FhirStoreTestsR4BObservation.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Models;
using FhirServerHarness.Storage;
using FhirServerHarness.Tests.Extensions;
using FhirServerHarness.Tests.Models;
using FluentAssertions;
using System.Net;
using System.Text.Json;
using Xunit.Abstractions;

namespace FhirServerHarness.Tests;

/// <summary>Unit tests FhirStore Observation / search functionality.</summary>
public class FhirStoreTestsR4BObservation: IDisposable
{
    /// <summary>The FHIR store.</summary>
    private static IFhirStore _store;

    /// <summary>(Immutable) The configuration.</summary>
    private static readonly ProviderConfiguration _config = new()
    {
        FhirVersion = Hl7.Fhir.Model.FHIRVersion.N4_1,
        TenantRoute = "r4b",
        BaseUrl = "http://localhost:5101/r4b",
    };

    /// <summary>(Immutable) The total observations expected.</summary>
    private const int _expectedTotal = 6;

    /// <summary>(Immutable) The expected vital signs.</summary>
    private const int _expectedVitalSigns = 3;

    /// <summary>(Immutable) The expected subject example.</summary>
    private const int _expectedSubjectExample = 4;

    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>
    /// Initializes static members of the FhirServerHarness.Tests.FhirStoreTestsR4BObservation
    /// class.
    /// </summary>
    static FhirStoreTestsR4BObservation()
    {
        _store = new VersionedFhirStore();
        _store.Init(_config);

        string path = Path.GetRelativePath(Directory.GetCurrentDirectory(), "data/r4b");

        foreach (string filename in Directory.EnumerateFiles(path, "Observation-*.json", SearchOption.TopDirectoryOnly))
        {
            _ = _store.InstanceCreate(
                "Observation",
                File.ReadAllText(filename),
                "application/fhir+json",
                "application/fhir+json",
                string.Empty,
                true,
                out _,
                out _,
                out _,
                out _,
                out _);
        }

        foreach (string filename in Directory.EnumerateFiles(path, "searchparameter-observation*.json", SearchOption.TopDirectoryOnly))
        {
            _ = _store.InstanceCreate(
                "SearchParameter",
                File.ReadAllText(filename),
                "application/fhir+json",
                "application/fhir+json",
                string.Empty,
                true,
                out _,
                out _,
                out _,
                out _,
                out _);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FhirStoreTestsR4B"/> class.
    /// </summary>
    /// <param name="testOutputHelper">The test output helper.</param>
    public FhirStoreTestsR4BObservation(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged
    /// resources.
    /// </summary>
    public void Dispose()
    {
        // cleanup
    }

    [Theory]
    [InlineData("_id=example", 1)]
    [InlineData("_id=AnIdThatDoesNotExist", 0)]
    [InlineData("_id:not=example", (_expectedTotal - 1))]
    [InlineData("value-quantity=185|http://unitsofmeasure.org|[lb_av]", 1)]
    [InlineData("value-quantity=185|http://unitsofmeasure.org|lbs", 1)]
    [InlineData("value-quantity=185||[lb_av]", 1)]
    [InlineData("value-quantity=185||lbs", 1)]
    [InlineData("value-quantity=185", 1)]
    [InlineData("value-quantity=ge185|http://unitsofmeasure.org|[lb_av]", 1)]
    [InlineData("value-quantity=ge185||[lb_av]", 1)]
    [InlineData("value-quantity=ge185||lbs", 1)]
    [InlineData("value-quantity=ge185", 2)]
    [InlineData("value-quantity=gt185|http://unitsofmeasure.org|[lb_av]", 0)]
    [InlineData("value-quantity=gt185||[lb_av]", 0)]
    [InlineData("value-quantity=gt185||lbs", 0)]
    [InlineData("value-quantity=84.1|http://unitsofmeasure.org|[kg]", 0)]       // test unit conversion
    [InlineData("value-quantity=820|urn:iso:std:iso:11073:10101|265201", 1)]
    [InlineData("value-quantity=820|urn:iso:std:iso:11073:10101|cL/s", 1)]
    [InlineData("value-quantity=820|urn:iso:std:iso:11073:10101|cl/s", 1)]
    [InlineData("value-quantity=820||265201", 1)]
    [InlineData("value-quantity=820||cL/s", 1)]
    [InlineData("subject=Patient/example", _expectedSubjectExample)]
    [InlineData("subject=Patient/UnknownPatientId", 0)]
    [InlineData("subject=example", _expectedSubjectExample)]
    [InlineData("code=http://loinc.org|9272-6", 1)]
    [InlineData("code=http://snomed.info/sct|169895004", 1)]
    [InlineData("code=http://snomed.info/sct|9272-6", 0)]
    [InlineData("_profile=http://hl7.org/fhir/StructureDefinition/vitalsigns", _expectedVitalSigns)]
    [InlineData("_profile:missing=true", (_expectedTotal - _expectedVitalSigns))]
    [InlineData("_profile:missing=false", _expectedVitalSigns)]
    public void ObservationSearchWithCount(string search, int matchCount)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        _store.TypeSearch("Observation", search, "application/fhir+json", out string bundle, out _);

        bundle.Should().NotBeNullOrEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(bundle);

        results.Should().NotBeNull();
        results!.Total.Should().Be(matchCount);

        //_testOutputHelper.WriteLine(bundle);
    }

}
﻿// <copyright file="SubscriptionConverter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Globalization;
using FhirStore.Extensions;
using FhirStore.Models;
using Hl7.Fhir.Model;
using static FhirStore.Versioned.Shims.Subscriptions.ConverterUtils;

namespace FhirStore.Versioned.Shims.Subscriptions;

/// <summary>A FHIR R4 subscription format converter.</summary>
public class SubscriptionConverter
{
    /// <summary>(Immutable) Backport filter criteria.</summary>
    private const string _filterCriteria = "backport-filter-criteria";

    /// <summary>(Immutable) Backport heartbeat period.</summary>
    private const string _heartbeatPeriod = "backport-heartbeat-period";

    /// <summary>(Immutable) Backport timeout.</summary>
    private const string _timeout = "backport-timeout";

    /// <summary>(Immutable) Backport maximum count.</summary>
    private const string _maxCount = "backport-max-count";

    /// <summary>(Immutable) Backport channel type.</summary>
    private const string _channelType = "backport-channel-type";

    /// <summary>(Immutable) Backport content.</summary>
    private const string _content = "backport-payload-content";

    /// <summary>Attempts to parse a ParsedSubscription from the given object.</summary>
    /// <param name="subscription">The subscription.</param>
    /// <param name="common">      [out] The common.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryParse(object subscription, out ParsedSubscription common)
    {
        if ((subscription == null) ||
            (subscription is not Hl7.Fhir.Model.Subscription sub) ||
            string.IsNullOrEmpty(sub.Id) ||
            string.IsNullOrEmpty(sub.Criteria))
        {
            common = null!;
            return false;
        }

        common = new()
        {
            Id = sub.Id,
            TopicUrl = sub.Criteria,
            ChannelSystem = string.Empty,
            ChannelCode = sub.Channel.Type == null
                ? string.Empty
                : Hl7.Fhir.Utility.EnumUtility.GetLiteral(sub.Channel.Type),
            Endpoint = sub.Channel.Endpoint ?? string.Empty,
            ContentType = sub.Channel.Payload?.ToString() ?? string.Empty,
        };

        Hl7.Fhir.Model.Extension? ext;
        IEnumerable<Hl7.Fhir.Model.Extension> exts;
        Dictionary<string, List<Hl7.Fhir.Model.DataType>> parsedExts;
        Dictionary<string, List<List<Hl7.Fhir.Model.Extension>>> nested;
        string stringVal;

        // parse channel extensions
        ParseExtensions(sub.Channel.Extension, out parsedExts, out nested);

        if ((!string.IsNullOrEmpty(stringVal = GetString(parsedExts, _heartbeatPeriod))) &&
            int.TryParse(stringVal, out int heartbeat))
        {
            common.HeartbeatSeconds = heartbeat;
        }

        if ((!string.IsNullOrEmpty(stringVal = GetString(parsedExts, _timeout))) &&
            int.TryParse(stringVal, out int timeout))
        {
            common.TimeoutSeconds = timeout;
        }

        if ((!string.IsNullOrEmpty(stringVal = GetString(parsedExts, _maxCount))) &&
            int.TryParse(stringVal, out int maxCount))
        {
            common.MaxEventsPerNotification = maxCount;
        }

        ext = sub.Channel.TypeElement.GetExtension(_urlBackport + _channelType);
        if ((ext != null) &&
            (ext.Value != null) &&
            (ext.Value is Hl7.Fhir.Model.Coding coding))
        {
            common.ChannelSystem = coding.System;
            common.ChannelCode = coding.Code;
        }

        ext = sub.Channel.PayloadElement.GetExtension(_urlBackport + _content);
        if ((ext != null) &&
            (ext.Value != null) &&
            (ext.Value is Hl7.Fhir.Model.Code code))
        {
            common.ContentLevel = code.Value;
        }

        // add parameters
        if (sub.Channel.Header?.Any() ?? false)
        {
            foreach (string header in sub.Channel.Header)
            {
                int index = header.IndexOf(':');
                if (index == -1)
                {
                    index = header.IndexOf('=');
                }

                if (index == -1)
                {
                    continue;
                }

                string key = header.Substring(0, index).Trim();
                string value = header.Substring(index + 1).Trim();

                if (!common.Parameters.ContainsKey(key))
                {
                    common.Parameters.Add(key, new());
                }

                common.Parameters[key].Add(value.ToString());
            }
        }

        // add filters
        exts = sub.CriteriaElement.GetExtensions(_urlBackport + _filterCriteria);
        if (exts?.Any() ?? false)
        {
            foreach (string criteria in exts.Select(e => e.Value.ToString() ?? string.Empty))
            {
                if (string.IsNullOrEmpty(criteria))
                {
                    continue;
                }

                string key;
                string resourceType;
                string value;

                int index = criteria.IndexOf('?');
                if (index == -1)
                {
                    key = "-";
                    resourceType = string.Empty;
                    value = criteria;
                }
                else
                {
                    key = criteria.Substring(0, index);
                    resourceType = key;
                    value = criteria.Substring(index + 1);
                }

                if (!common.Filters.ContainsKey(key))
                {
                    common.Filters.Add(key, new());
                }

                string[] queryParams = value.Split('&');
                
                foreach (string queryParam in queryParams)
                {
                    string[] components = queryParam.Split('=');

                    if (components.Length != 2)
                    {
                        continue;
                    }

                    string[] keyComponents = components[0].Split(":");

                    common.Filters[key].Add(new(
                        resourceType,
                        keyComponents[0],
                        string.Empty,                               // TODO: figure out prefix based on type info we don't have here
                        (keyComponents.Length > 1) ? keyComponents[1] : string.Empty,
                        components[1]));
                }
            }
        }

        return true;
    }

    /// <summary>Gets the not events in this collection.</summary>
    /// <param name="status">The status.</param>
    /// <returns>
    /// An enumerator that allows foreach to be used to process the not events in this collection.
    /// </returns>
    private IEnumerable<ParsedSubscriptionStatus.ParsedNotificationEvent> GetNotEvents(
        Dictionary<string, List<List<Hl7.Fhir.Model.Parameters.ParameterComponent>>> nested)
    {
        if (!nested.ContainsKey("notification-event"))
        {
            return Array.Empty<ParsedSubscriptionStatus.ParsedNotificationEvent>();
        }

        List<ParsedSubscriptionStatus.ParsedNotificationEvent> eventList = new();

        foreach (List<Hl7.Fhir.Model.Parameters.ParameterComponent> notEventParams in nested["notification-event"])
        {
            ParseParameters(
                notEventParams,
                out Dictionary<string, List<Hl7.Fhir.Model.DataType>> values,
                out _);

            eventList.Add(new ParsedSubscriptionStatus.ParsedNotificationEvent()
            {
                Id = string.Empty,
                EventNumber = long.TryParse(GetString(values, "event-number"), out long en)
                            ? en
                            : null,
                Timestamp = DateTimeOffset.TryParse(GetString(values, "timestamp"), null, DateTimeStyles.RoundtripKind, out DateTimeOffset dt)
                            ? dt
                            : null,
                FocusReference = GetString(values, "focus"),
                AdditionalContextReferences = GetStrings(values, "additional-context"),
            });
        }

        return eventList.ToArray();
    }

    /// <summary>Attempts to parse a ParsedSubscriptionStatus from the given object.</summary>
    /// <param name="subscriptionStatus">The subscription.</param>
    /// <param name="bundleId">          Identifier for the bundle.</param>
    /// <param name="common">            [out] The common.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryParse(object subscriptionStatus, string bundleId, out ParsedSubscriptionStatus common)
    {
        if ((subscriptionStatus == null) ||
            (subscriptionStatus is not Hl7.Fhir.Model.Parameters status))
        {
            common = null!;
            return false;
        }

        ParseParameters(
            status.Parameter,
            out Dictionary<string, List<Hl7.Fhir.Model.DataType>> values,
            out Dictionary<string, List<List<Hl7.Fhir.Model.Parameters.ParameterComponent>>> nested);

        common = new()
        {
            BundleId = bundleId,
            SubscriptionReference = GetString(values, "subscription"),
            SubscriptionTopicCanonical = GetString(values, "topic"),
            Status = GetString(values, "status"),
            NotificationType =
                GetString(values, "type").TryFhirEnum(out ParsedSubscription.NotificationTypeCodes nt)
                ? nt
                : null,
            EventsSinceSubscriptionStart = long.TryParse(GetString(values, "events-since-subscription-start"), out long count)
                ? count
                : null,
            NotificationEvents = GetNotEvents(nested),
        };

        return true;
    }

    /// <summary>Status for subscription.</summary>
    /// <param name="subscription">    The subscription.</param>
    /// <param name="notificationType">Type of the notification.</param>
    /// <param name="baseUrl">         URL of the base.</param>
    /// <returns>A Hl7.Fhir.Model.Resource.</returns>
    public Hl7.Fhir.Model.Resource StatusForSubscription(
        ParsedSubscription subscription,
        string notificationType,
        /// <summary>Gets the base url)</summary>
        string baseUrl)
    {
        return new Hl7.Fhir.Model.Parameters()
        {
            Id = Guid.NewGuid().ToString(),
            Parameter = new()
            {
                new()
                {
                    Name = "subscription",
                    Value = new FhirString(baseUrl + "/Subscription/" + subscription.Id)
                },
                new()
                {
                    Name = "topic",
                    Value = new Canonical(subscription.TopicUrl),
                },
                new()
                {
                    Name = "status",
                    Value = new Code(subscription.CurrentStatus)
                },
                new()
                {
                    Name = "type",
                    Value = new Code(notificationType),
                },
                new()
                {
                    Name = "events-since-subscription-start",
                    Value = new FhirString(subscription.CurrentEventCount.ToString())
                },
            },
        };
    }

    /// <summary>
    /// Build a bundle of subscription events into the desired format and content level.
    /// </summary>
    /// <param name="subscription">    The subscription the events belong to.</param>
    /// <param name="eventNumbers">    One or more event numbers to include.</param>
    /// <param name="notificationType">Type of notification (e.g., 'notification-event')</param>
    /// <param name="contentLevel">    Override for the content level specified in the subscription.</param>
    /// <returns></returns>
    public Bundle? BundleForSubscriptionEvents(
        ParsedSubscription subscription,
        IEnumerable<long> eventNumbers,
        string notificationType,
        string baseUrl,
        /// <summary>The content level.</summary>
        string contentLevel = "")
    {
        if (string.IsNullOrEmpty(contentLevel))
        {
            contentLevel = subscription.ContentLevel;
        }

        // create our notification bundle
        Bundle bundle = new()
        {
            Id = Guid.NewGuid().ToString(),
            Type = Bundle.BundleType.History,
            Timestamp = DateTimeOffset.Now,
            Entry = new(),
        };

        // create our status parameters
        Parameters status = (Parameters)StatusForSubscription(subscription, notificationType, baseUrl);

        // add our status to the bundle
        bundle.AddResourceEntry(status, $"urn:uuid:{status.Id}");

        HashSet<string> addedResources = new();

        bool isEmpty = contentLevel.Equals("empty", StringComparison.Ordinal);
        bool isFullResource = contentLevel.Equals("full-resource", StringComparison.Ordinal);

        // determine behavior of no event numbers
        if (!eventNumbers.Any())
        {
            // query-event should send all
            if (notificationType.Equals("query-event"))
            {
                eventNumbers = subscription.GeneratedEvents.Keys;
            }
            // others send the most recent if there is one
            else if (subscription.GeneratedEvents.Any())
            {
                eventNumbers = new long[] { subscription.GeneratedEvents.Keys.Last() };
            }
            else
            {
                eventNumbers = Array.Empty<long>();
            }
        }

        // iterate over the events
        foreach (long eventNumber in eventNumbers)
        {
            if (!subscription.GeneratedEvents.ContainsKey(eventNumber))
            {
                continue;
            }

            SubscriptionEvent se = subscription.GeneratedEvents[eventNumber];

            // check for empty notifications
            if (isEmpty)
            {
                // add just this event number to our status
                status.Parameter.Add(new()
                {
                    Name = "notification-event",
                    Part = new()
                    {
                        new()
                        {
                            Name = "event-number",
                            Value = new FhirString(eventNumber.ToString()),
                        },
                        new()
                        {
                            Name = "timestamp",
                            Value = new Instant(se.Timestamp),
                        }
                    },
                });

                continue;
            }

            Resource r = (Resource)se.Focus;
            string relativeUrl = $"{r.TypeName}/{r.Id}";

            // add this event to our status
            Parameters.ParameterComponent ne = new()
            {
                Name = "notification-event",
                Part = new()
                {
                    new()
                    {
                        Name = "event-number",
                        Value = new FhirString(eventNumber.ToString()),
                    },
                    new()
                    {
                        Name = "timestamp",
                        Value = new Instant(se.Timestamp),
                    },
                    new()
                    {
                        Name = "focus",
                        Value = new ResourceReference(baseUrl + "/" + relativeUrl)
                    }
                },
            };

            if (se.AdditionalContext?.Any() ?? false)
            {
                ne.Part.AddRange(
                    se.AdditionalContext.Select(o => new Parameters.ParameterComponent()
                    {
                        Name = "additional-context",
                        Value = new ResourceReference($"{((Resource)o).TypeName}/{((Resource)o).Id}"),
                    }));
            }

            if (!addedResources.Contains(relativeUrl))
            {
                addedResources.Add(relativeUrl);

                if (isFullResource)
                {
                    bundle.Entry.Add(new Bundle.EntryComponent()
                    {
                        FullUrl = baseUrl + "/" + relativeUrl,
                        Resource = isFullResource ? r : null,
                    });
                }
            }

            // add any additional context
            if (se.AdditionalContext?.Any() ?? false)
            {
                foreach (object ac in se.AdditionalContext)
                {
                    Resource acr = (Resource)ac;
                    string acrRelative = $"{acr.TypeName}/{acr.Id}";

                    if (!addedResources.Contains(acrRelative))
                    {
                        addedResources.Add(acrRelative);

                        if (isFullResource)
                        {
                            bundle.Entry.Add(new Bundle.EntryComponent()
                            {
                                FullUrl = baseUrl + "/" + acrRelative,
                                Resource = isFullResource ? acr : null,
                            });
                        }
                    }
                }
            }

            status.Parameter.Add(ne);
        }

        // update the status information in our bundle
        bundle.Entry[0].Resource = status;

        return bundle;
    }
}

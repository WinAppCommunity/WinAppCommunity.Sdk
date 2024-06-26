﻿using Newtonsoft.Json;
using OwlCore.ComponentModel;
using WinAppCommunity.Sdk.Nomad.Serialization;

namespace WinAppCommunity.Sdk.Nomad.UpdateEvents;

[JsonConverter(typeof(UpdateEventJsonConverter))]
public abstract record WinAppCommunityUpdateEvent(string Id, string EventId) : IHasId;

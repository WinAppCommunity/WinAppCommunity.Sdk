﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;
using Ipfs;
using OwlCore.Extensions;
using OwlCore.Kubo;
using OwlCore.Nomad;
using OwlCore.Nomad.Extensions;
using OwlCore.Storage;
using WinAppCommunity.Sdk.Models;
using WinAppCommunity.Sdk.Nomad;
using WinAppCommunity.Sdk.Nomad.Kubo;
using WinAppCommunity.Sdk.Nomad.Kubo.Extensions;
using WinAppCommunity.Sdk.Nomad.UpdateEvents;

namespace WinAppCommunity.Sdk;

/// <summary>
/// Creates a new instance of <see cref="ModifiablePublisher"/>.
/// </summary>
/// <param name="listeningEventStreamHandlers">A shared collection of all available event streams that should participate in playback of events using their respective <see cref="IEventStreamHandler{TEventStreamEntry}.TryAdvanceEventStreamAsync"/>. </param>
public class ReadOnlyPublisher(
    ICollection<ISharedEventStreamHandler<Cid, KuboNomadEventStream, KuboNomadEventStreamEntry>>
        listeningEventStreamHandlers)
    : ReadOnlyPublisherNomadKuboEventStreamHandler(listeningEventStreamHandlers), IReadOnlyPublisher
{
    /// <inheritdoc />
    public string Name => Inner.Name;

    /// <inheritdoc />
    public string Description => Inner.Description;

    /// <inheritdoc />
    public string? AccentColor => Inner.AccentColor;

    /// <inheritdoc />
    public Link[] Links => Inner.Links;

    /// <inheritdoc />
    public EmailConnection? ContactEmail => Inner.ContactEmail;

    /// <inheritdoc />
    public bool IsPrivate => Inner.IsPrivate;

    /// <inheritdoc />
    public event EventHandler<string>? NameUpdated;

    /// <inheritdoc />
    public event EventHandler<string>? DescriptionUpdated;

    /// <inheritdoc />
    public event EventHandler<string?>? AccentColorUpdated;

    /// <inheritdoc />
    public event EventHandler<Link[]>? LinksUpdated;

    /// <inheritdoc />
    public event EventHandler<EmailConnection?>? ContactEmailUpdated;

    /// <inheritdoc />
    public event EventHandler<bool>? IsPrivateUpdated;

    /// <inheritdoc />
    public async Task<IReadOnlyUser> GetOwnerAsync(CancellationToken cancellationToken)
    {
        var existingKeysEnumerable = await Client.Key.ListAsync(cancellationToken);
        var existingKeys = existingKeysEnumerable.ToOrAsList();
        
        // If current node has write permissions
        if (existingKeys.FirstOrDefault(x => x.Id == Inner.Owner) is { } existingKey)
        {
            var appModel = new ModifiableUser(ListeningEventStreamHandlers)
            {
                Client = Client,
                Id = Inner.Owner,
                Sources = Sources,
                KuboOptions = KuboOptions,
                LocalEventStreamKeyName = LocalEventStreamKeyName,
                RoamingKeyName = existingKey.Name,
            };

            await appModel.AdvanceEventStreamToAtLeastAsync(EventStreamPosition?.TimestampUtc ?? DateTime.UtcNow,
                (cid, ct) =>
                    NomadKuboEventStreamHandlerExtensions.ContentPointerToStreamEntryAsync(cid, Client,
                        KuboOptions.UseCache, ct), cancellationToken).ToListAsync(cancellationToken);

            _ = appModel.PublishRoamingAsync<ModifiableUser, UserUpdateEvent, User>(cancellationToken);

            return appModel;
        }
        // If current node has no write permissions
        else
        {
            var appModel = new ReadOnlyUser(ListeningEventStreamHandlers)
            {
                Client = Client,
                Id = Inner.Owner,
                KuboOptions = KuboOptions,
                Sources = Sources,
                LocalEventStreamKeyName = LocalEventStreamKeyName,
            };

            await appModel.AdvanceEventStreamToAtLeastAsync(EventStreamPosition?.TimestampUtc ?? DateTime.UtcNow,
                    (cid, ct) =>
                        NomadKuboEventStreamHandlerExtensions.ContentPointerToStreamEntryAsync(cid, Client,
                            KuboOptions.UseCache, ct), cancellationToken)
                .ToListAsync(cancellationToken);

            return appModel;
        }
    }

    /// <summary>
    /// Gets the icon file for this user.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public Task<IFile?> GetIconFileAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var iconCid = Inner.Icon;
        if (iconCid is null)
            return Task.FromResult<IFile?>(null);

        return Task.FromResult<IFile?>(new IpfsFile(iconCid, $"{nameof(User)}.{Id}.png", Client));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<IReadOnlyUser> GetUsersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var existingKeysEnumerable = await Client.Key.ListAsync(cancellationToken);
        var existingKeys = existingKeysEnumerable.ToOrAsList();

        foreach (var userCid in Inner.Users)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // If current node has write permissions
            if (existingKeys.FirstOrDefault(x => x.Id == userCid) is { } existingKey)
            {
                var appModel = new ModifiableUser(ListeningEventStreamHandlers)
                {
                    Client = Client,
                    Id = userCid,
                    Sources = Sources,
                    KuboOptions = KuboOptions,
                    LocalEventStreamKeyName = LocalEventStreamKeyName,
                    RoamingKeyName = existingKey.Name,
                };

                await appModel.AdvanceEventStreamToAtLeastAsync(EventStreamPosition?.TimestampUtc ?? DateTime.UtcNow,
                    (cid, ct) =>
                        NomadKuboEventStreamHandlerExtensions.ContentPointerToStreamEntryAsync(cid, Client,
                            KuboOptions.UseCache, ct), cancellationToken).ToListAsync(cancellationToken);

                _ = appModel.PublishRoamingAsync<ModifiableUser, UserUpdateEvent, User>(cancellationToken);
                
                yield return appModel;
            }
            // If current node has no write permissions
            else
            {
                var appModel = new ReadOnlyUser(ListeningEventStreamHandlers)
                {
                    Client = Client,
                    Id = userCid,
                    KuboOptions = KuboOptions,
                    Sources = Sources,
                    LocalEventStreamKeyName = LocalEventStreamKeyName,
                };

                await appModel.AdvanceEventStreamToAtLeastAsync(EventStreamPosition?.TimestampUtc ?? DateTime.UtcNow,
                        (cid, ct) =>
                            NomadKuboEventStreamHandlerExtensions.ContentPointerToStreamEntryAsync(cid, Client,
                                KuboOptions.UseCache, ct), cancellationToken)
                    .ToListAsync(cancellationToken);

                yield return appModel;
            }
        }
    }

    /// <summary>
    /// Get the projects for this user.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public async IAsyncEnumerable<IReadOnlyProject> GetProjectsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var existingKeysEnumerable = await Client.Key.ListAsync(cancellationToken);
        var existingKeys = existingKeysEnumerable.ToOrAsList();

        foreach (var projectCid in Inner.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (result, _) =
                await Client.ResolveDagCidAsync<Project>(projectCid, nocache: !KuboOptions.UseCache, cancellationToken);
            Guard.IsNotNull(result);

            // assuming cid is ipns and won't change
            var ipnsId = projectCid;

            // If current node has write permissions
            if (existingKeys.FirstOrDefault(x => x.Id == ipnsId) is { } existingKey)
            {
                var appModel = new ModifiableProject(ListeningEventStreamHandlers)
                {
                    Client = Client,
                    Id = ipnsId,
                    Sources = Sources,
                    KuboOptions = KuboOptions,
                    Inner = result,
                    LocalEventStreamKeyName = LocalEventStreamKeyName,
                    RoamingKeyName = existingKey.Name,
                };

                await appModel.AdvanceEventStreamToAtLeastAsync(EventStreamPosition?.TimestampUtc ?? DateTime.UtcNow,
                    (cid, ct) =>
                        NomadKuboEventStreamHandlerExtensions.ContentPointerToStreamEntryAsync(cid, Client,
                            KuboOptions.UseCache, ct), cancellationToken).ToListAsync(cancellationToken);

                _ = appModel.PublishRoamingAsync<ModifiableProject, ProjectUpdateEvent, Project>(cancellationToken);

                yield return appModel;
            }
            // If current node has no write permissions
            else
            {
                var appModel = new ReadOnlyProject(ListeningEventStreamHandlers)
                {
                    Client = Client,
                    Id = ipnsId,
                    Inner = result,
                    KuboOptions = KuboOptions,
                    Sources = Sources,
                    LocalEventStreamKeyName = LocalEventStreamKeyName,
                };

                await appModel.AdvanceEventStreamToAtLeastAsync(EventStreamPosition?.TimestampUtc ?? DateTime.UtcNow,
                    (cid, ct) =>
                        NomadKuboEventStreamHandlerExtensions.ContentPointerToStreamEntryAsync(cid, Client,
                            KuboOptions.UseCache, ct), cancellationToken).ToListAsync(cancellationToken);
                yield return appModel;
            }
        }
    }

    /// <summary>
    /// Get the child publishers for this publisher.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public async IAsyncEnumerable<IReadOnlyPublisher> GetChildPublishersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var existingKeysEnumerable = await Client.Key.ListAsync(cancellationToken);
        var existingKeys = existingKeysEnumerable.ToOrAsList();

        foreach (var publisherCid in Inner.ChildPublishers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (result, _) =
                await Client.ResolveDagCidAsync<Publisher>(publisherCid, nocache: !KuboOptions.UseCache,
                    cancellationToken);
            Guard.IsNotNull(result);

            // assuming cid is ipns and won't change
            var ipnsId = publisherCid;

            // If current node has write permissions
            if (existingKeys.FirstOrDefault(x => x.Id == ipnsId) is { } existingKey)
            {
                var appModel = new ModifiablePublisher(ListeningEventStreamHandlers)
                {
                    Client = Client,
                    Id = ipnsId,
                    Sources = Sources,
                    KuboOptions = KuboOptions,
                    Inner = result,
                    LocalEventStreamKeyName = LocalEventStreamKeyName,
                    RoamingKeyName = existingKey.Name,
                };

                await appModel.AdvanceEventStreamToAtLeastAsync(EventStreamPosition?.TimestampUtc ?? DateTime.UtcNow,
                    (cid, ct) =>
                        NomadKuboEventStreamHandlerExtensions.ContentPointerToStreamEntryAsync(cid, Client,
                            KuboOptions.UseCache, ct), cancellationToken).ToListAsync(cancellationToken);

                _ = appModel.PublishRoamingAsync<ModifiablePublisher, PublisherUpdateEvent, Publisher>(cancellationToken);
                
                yield return appModel;
            }
            // If current node has no write permissions
            else
            {
                var appModel = new ReadOnlyPublisher(ListeningEventStreamHandlers)
                {
                    Client = Client,
                    Id = ipnsId,
                    Inner = result,
                    KuboOptions = KuboOptions,
                    Sources = Sources,
                    LocalEventStreamKeyName = LocalEventStreamKeyName,
                };

                await appModel.AdvanceEventStreamToAtLeastAsync(EventStreamPosition?.TimestampUtc ?? DateTime.UtcNow,
                    (cid, ct) =>
                        NomadKuboEventStreamHandlerExtensions.ContentPointerToStreamEntryAsync(cid, Client,
                            KuboOptions.UseCache, ct), cancellationToken).ToListAsync(cancellationToken);
                yield return appModel;
            }
        }
    }

    /// <summary>
    /// Get the parent publishers for this publisher.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public async IAsyncEnumerable<IReadOnlyPublisher> GetParentPublishersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var existingKeysEnumerable = await Client.Key.ListAsync(cancellationToken);
        var existingKeys = existingKeysEnumerable.ToOrAsList();

        foreach (var publisherCid in Inner.ParentPublishers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (result, _) =
                await Client.ResolveDagCidAsync<Publisher>(publisherCid, nocache: !KuboOptions.UseCache,
                    cancellationToken);
            Guard.IsNotNull(result);

            // assuming cid is ipns and won't change
            var ipnsId = publisherCid;

            // If current node has write permissions
            if (existingKeys.FirstOrDefault(x => x.Id == ipnsId) is { } existingKey)
            {
                var appModel = new ModifiablePublisher(ListeningEventStreamHandlers)
                {
                    Client = Client,
                    Id = ipnsId,
                    Sources = Sources,
                    KuboOptions = KuboOptions,
                    Inner = result,
                    LocalEventStreamKeyName = LocalEventStreamKeyName,
                    RoamingKeyName = existingKey.Name,
                };

                await appModel.AdvanceEventStreamToAtLeastAsync(EventStreamPosition?.TimestampUtc ?? DateTime.UtcNow,
                    (cid, ct) =>
                        NomadKuboEventStreamHandlerExtensions.ContentPointerToStreamEntryAsync(cid, Client,
                            KuboOptions.UseCache, ct), cancellationToken).ToListAsync(cancellationToken);

                _ = appModel.PublishRoamingAsync<ModifiablePublisher, PublisherUpdateEvent, Publisher>(cancellationToken);
                
                yield return appModel;
            }
            // If current node has no write permissions
            else
            {
                var appModel = new ReadOnlyPublisher(ListeningEventStreamHandlers)
                {
                    Client = Client,
                    Id = ipnsId,
                    Inner = result,
                    KuboOptions = KuboOptions,
                    Sources = Sources,
                    LocalEventStreamKeyName = LocalEventStreamKeyName,
                };

                await appModel.AdvanceEventStreamToAtLeastAsync(EventStreamPosition?.TimestampUtc ?? DateTime.UtcNow,
                    (cid, ct) =>
                        NomadKuboEventStreamHandlerExtensions.ContentPointerToStreamEntryAsync(cid, Client,
                            KuboOptions.UseCache, ct), cancellationToken).ToListAsync(cancellationToken);
                yield return appModel;
            }
        }
    }
}
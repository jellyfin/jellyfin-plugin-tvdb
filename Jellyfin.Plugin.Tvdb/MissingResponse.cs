using System;
using System.Collections.ObjectModel;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.Tvdb;

/// <summary>
/// The output Structure that is returned.
/// </summary>
public class MissingResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MissingResponse"/> class.
    /// </summary>
    /// <param name="item">summery.</param>
    public MissingResponse(BaseItem item)
    {
        Id = item.Id;
        Type = item.GetType().Name;
        Title = item.Name;
        Index = item.IndexNumber;
        Child = new Collection<MissingResponse>();
        Missing = false;
    }

    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it is missing.
    /// </summary>
    public bool Missing { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the index.
    /// </summary>
    public int? Index { get; set; }

    /// <summary>
    /// Gets the children.
    /// </summary>
    public Collection<MissingResponse> Child { get; }
}

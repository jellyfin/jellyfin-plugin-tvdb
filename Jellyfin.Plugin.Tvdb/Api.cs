using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Mime;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Tvdb;

/// <summary>
/// Gets missing chapters.
/// </summary>
[ApiController]
// [Authorize]
[Route("/thetvdb")]
[Produces(MediaTypeNames.Application.Json)]
public class Api : ControllerBase
{
    private readonly IItemRepository _iItemRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="Api"/> class.
    /// </summary>
    /// <param name="iItemRepository">empty.</param>
    public Api(IItemRepository iItemRepository)
    {
        this._iItemRepository = iItemRepository;
    }

    /// <summary>
    /// The Api path /missing/Missing.
    /// </summary>
    /// <returns>this.</returns>
    [HttpGet("Missing")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Missing()
    {
        var query = new InternalItemsQuery { IsMissing = true, IsUnaired = false };
        var res = _iItemRepository.GetItemList(query);
        Collection<MissingResponse> dict = new Collection<MissingResponse>();
        var temp = res.Select(GenerateChain).ToList();
        foreach (var v in temp)
        {
            MissingResponse? response = null;
            foreach (var item in v)
            {
                Collection<MissingResponse> insert_into = response == null ? dict : response.Child;

                MissingResponse? i = insert_into.FirstOrDefault(it => it.Id == item.Id);
                if (i == null)
                {
                    insert_into.Add(item);
                    response = item;
                }
                else
                {
                    if (item.Missing && i.Missing == false)
                    {
                        i.Missing = true;
                    }

                    response = i;
                }
            }
        }

        return Ok(dict);
    }

    private List<MissingResponse> GenerateChain(BaseItem baseItem)
    {
        List<MissingResponse> list = new List<MissingResponse>();
        BaseItem last = baseItem;
        while (true)
        {
            list.Add(new MissingResponse(last));
            last = last.GetParent();
            if (last == null || last.GetType().Name == "AggregateFolder")
            {
                break;
            }
        }

        list[0].Missing = true;
        list.Reverse();
        return list;
    }
}

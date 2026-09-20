using FluentAssertions;
using Resonance.Core.Helpers;
using Resonance.Core.Models;
using Resonance.Core.Services.Data;
using Xunit;

namespace Resonance.Core.Tests;

public class SortOrderHelperTests
{
    [Fact]
    public void SmartPlaylistSortOrders_MapToEquivalentSongSortOrders()
    {
        foreach (var smartPlaylistSortOrder in Enum.GetValues<SmartPlaylistSortOrder>())
        {
            var songSortOrder = SortOrderHelper.MapToSongSortOrder(smartPlaylistSortOrder);

            songSortOrder.ToString().Should().Be(smartPlaylistSortOrder.ToString());
        }
    }

    [Fact]
    public void SongSortOrders_MapToEquivalentSmartPlaylistSortOrders()
    {
        foreach (var songSortOrder in Enum.GetValues<SongSortOrder>().Where(value => value != SongSortOrder.PlaylistOrder))
        {
            var smartPlaylistSortOrder = SortOrderHelper.MapToSmartPlaylistSortOrder(songSortOrder);

            smartPlaylistSortOrder.ToString().Should().Be(songSortOrder.ToString());
        }
    }

    [Fact]
    public void PlaylistOrder_MapsToTitleAscendingFallback()
    {
        SortOrderHelper.MapToSmartPlaylistSortOrder(SongSortOrder.PlaylistOrder)
            .Should().Be(SmartPlaylistSortOrder.TitleAsc);
    }
}

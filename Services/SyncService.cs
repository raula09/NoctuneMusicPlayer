using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MusicPlayerApp.Models;
using MusicPlayerApp.Views;

namespace MusicPlayerApp.Services
{
    public static class SyncService
    {
        private static readonly HttpClient http = new HttpClient();

        public static async Task SyncAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return;

            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            await PullFromServerAsync(token);
        }

        private static async Task PullFromServerAsync(string token)
        {
            try
            {
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var res = await http.GetAsync($"{ApiConfig.BaseUrl}/playlists");
                if (!res.IsSuccessStatusCode)
                    return;

                var list = await res.Content.ReadFromJsonAsync<List<PlaylistDto>>();
                if (list == null)
                    return;

                // No local caching anymore — server is the source of truth
                Console.WriteLine($"Synced {list.Count} playlists from server.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sync error: {ex.Message}");
            }
        }
    }
}

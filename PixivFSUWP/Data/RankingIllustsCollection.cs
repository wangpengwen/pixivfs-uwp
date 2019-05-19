﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace PixivFSUWP.Data
{
    public class RankingIllustsCollection : ObservableCollection<ViewModels.WaterfallItemViewModel>, ISupportIncrementalLoading
    {
        readonly string userID;
        string nexturl = "begin";
        bool _busy = false;
        bool _emergencyStop = false;
        EventWaitHandle pause = new ManualResetEvent(true);

        public RankingIllustsCollection(string UserID)
        {
            userID = UserID;
        }

        public RankingIllustsCollection() : this(OverAll.GlobalBaseAPI.UserID) { }

        public bool HasMoreItems
        {
            get => nexturl != "";
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            if (_busy)
                throw new InvalidOperationException("Only one operation in flight at a time");
            _busy = true;
            return AsyncInfo.Run((c) => LoadMoreItemsAsync(c, count));
        }

        public void StopLoading()
        {
            if (_busy)
            {
                _emergencyStop = true;
                ResumeLoading();
            }
        }

        public void PauseLoading()
        {
            pause.Reset();
        }

        public void ResumeLoading()
        {
            pause.Set();
        }

        protected async Task<LoadMoreItemsResult> LoadMoreItemsAsync(CancellationToken c, uint count)
        {
            try
            {
                if (!HasMoreItems) return new LoadMoreItemsResult() { Count = 0 };
                LoadMoreItemsResult toret = new LoadMoreItemsResult() { Count = 0 };
                JsonObject rankingres = null;
                if (nexturl == "begin")
                    rankingres = await new PixivCS
                        .PixivAppAPI(OverAll.GlobalBaseAPI)
                        .IllustRanking();
                else
                {
                    Uri next = new Uri(nexturl);
                    string getparam(string param) => HttpUtility.ParseQueryString(next.Query).Get(param);
                    rankingres = await new PixivCS
                        .PixivAppAPI(OverAll.GlobalBaseAPI)
                        .IllustRanking(Mode:getparam("mode"),Filter:getparam("filter"),Offset:getparam("offset"));
                }
                nexturl = rankingres["next_url"].TryGetString();
                foreach (var recillust in rankingres["illusts"].GetArray())
                {
                    if (_emergencyStop)
                    {
                        _emergencyStop = false;
                        throw new Exception();
                    }
                    await Task.Run(() => pause.WaitOne());
                    Data.WaterfallItem recommendi = Data.WaterfallItem.FromJsonValue(recillust.GetObject());
                    var recommendmodel = ViewModels.WaterfallItemViewModel.FromItem(recommendi);
                    await recommendmodel.LoadImageAsync();
                    Add(recommendmodel);
                    toret.Count++;
                }
                return toret;
            }
            finally
            {
                _busy = false;
            }
        }
    }
}
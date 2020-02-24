using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IBApi;

namespace Tws2UniFeeder
{
    public class EWrapperImpl : EWrapper
    {
        private readonly SubscriptionDictionary subscription;
        private readonly ConcurrentDictionary<string, Quote> quotes;
        private readonly IBackgroundQueue<Quote> queue;
        private readonly ITwsProcess tws;
        private readonly ILogger logger;
        //! [ewrapperimpl]
        private int nextOrderId;
        //! [socket_declare]
        EClientSocket clientSocket;
        public readonly EReaderSignal signal;
        //! [socket_declare]

        //! [socket_init]
        public EWrapperImpl(SubscriptionDictionary subscription, IBackgroundQueue<Quote> queue, ITwsProcess tws, ILoggerFactory loggerFactory)
        {
            this.subscription = subscription;
            this.logger = loggerFactory.CreateLogger<EWrapperImpl>();
            this.tws = tws;
            this.signal = new EReaderMonitorSignal();
            this.clientSocket = new EClientSocket(this, signal);
            this.queue = queue;
            this.quotes = new ConcurrentDictionary<string, Quote>();
        }
        //! [socket_init]

        public EClientSocket ClientSocket
        {
            get { return clientSocket; }
            set { clientSocket = value; }
        }

        public int NextOrderId
        {
            get { return nextOrderId; }
            set { nextOrderId = value; }
        }

        public string BboExchange { get; private set; }

        public virtual void error(Exception e)
        {
            logger.LogError("Exception thrown: {0}:{1}", e.GetType().Name, e.Message);

            RestartTwsProcess();

            throw e;
        }

        public virtual void error(string str)
        {
            logger.LogError("Interactive Brokers send error: " + str + "\n");
            this.ClientSocket.eDisconnect(resetState: true);

            RestartTwsProcess();
        }

        private void RestartTwsProcess()
        {
            if (tws.TwsProcessIsRunning())
                logger.LogInformation("TWS Process is running");
            else
                logger.LogInformation("TWS Process not running");

            if (!tws.RestartTwsProcess())
            {
                logger.LogError("Failed to restart the TVS. Manual restart required");
            }
        }

        //! [error]
        public virtual void error(int id, int errorCode, string errorMsg)
        {
            var loggerFormat = "{errorCode} requestId: {id} {symbol}: {errorMsg}";
            if (IsErrorCode(errorCode))
            {
                
                switch (errorCode)
                {
                    case 200:
                        subscription.ChangeStatusForRequest(id, RequestStatus.RequestFailed);
                        logger.LogError(loggerFormat, errorCode, id, subscription.GetSymbolNameByRequestId(id), errorMsg);
                        break;
                    case 354: 
                        subscription.ChangeStatusForRequest(id, RequestStatus.RequestFailed);
                        logger.LogError(loggerFormat, errorCode, id, subscription.GetSymbolNameByRequestId(id), errorMsg);
                        break;
                    case 502:
                        subscription.ReGenerateRequestIdForSymbol(id);
                        logger.LogError(loggerFormat, errorCode, id, subscription.GetSymbolNameByRequestId(id), errorMsg);
                        break;
                    case 504:
                        subscription.SetNotRequestedForAllSymbols();
                        this.ClientSocket.eDisconnect(resetState: true);
                        logger.LogError(loggerFormat, errorCode, id, subscription.GetSymbolNameByRequestId(id), errorMsg);
                        break;
                    case 10168:
                        subscription.ChangeStatusForRequest(id, RequestStatus.RequestFailed);
                        logger.LogError(loggerFormat, errorCode, id, subscription.GetSymbolNameByRequestId(id), errorMsg);
                        break;
                    case 10190:
                        subscription.ChangeStatusForRequest(id, RequestStatus.RequestFailed);
                        logger.LogError(loggerFormat, errorCode, id, subscription.GetSymbolNameByRequestId(id), errorMsg);
                        break;
                    case 10197:
                        // subscription.ChangeStatusForRequest(id, RequestStatus.RequestSuccess);
                        logger.LogError(loggerFormat, errorCode, id, subscription.GetSymbolNameByRequestId(id), errorMsg);
                        break;
                    default:
                        subscription.ChangeStatusForRequest(id, RequestStatus.RequestFailed);
                        logger.LogError(loggerFormat, errorCode, id, subscription.GetSymbolNameByRequestId(id), errorMsg);
                        // this.ClientSocket.eDisconnect(resetState: true);
                        break;
                }
            }
            else
            {
                logger.LogDebug(loggerFormat, errorCode, subscription.GetSymbolNameByRequestId(id), id, errorMsg);
            }
        }
        //! [error]

        private bool IsErrorCode(int error)
        {
            if (error >= 2000 && error < 3000)
                return false;

            return true;
        }

        public virtual void connectionClosed()
        {
            logger.LogInformation("Connection closed.\n");
        }

        public virtual void currentTime(long time)
        {
            // logger.LogInformation("Current Time: " + time + "\n");
        }

        //! [tickprice]
        public virtual void tickPrice(int tickerId, int field, double price, TickAttrib attribs)
        {
            if (field == (int)TickType.BidPrice || field == (int)TickType.AskPrice)
            {
                var symbol = subscription.GetSymbolNameByRequestId(tickerId);
                var tickType = (TickType)field;

                quotes.AddOrUpdate(symbol, s => new Quote
                {
                    Symbol = symbol,
                    Ask = tickType == TickType.AskPrice ? price : 0,
                    Bid = tickType == TickType.BidPrice ? price : 0
                }, (s, q) => new Quote
                {
                    Symbol = symbol,
                    Ask = tickType == TickType.AskPrice ? price : q.Ask,
                    Bid = tickType == TickType.BidPrice ? price : q.Bid
                });

                if (quotes.TryGetValue(symbol, out Quote quote))
                {
                    if (quote.IsFilled())
                    {
                        queue.QueueBackgroundWorkItem(quote);
                    }
                }
            }
        }
        //! [tickprice]

        //! [ticksize]
        public virtual void tickSize(int tickerId, int field, int size)
        {
            // logger.LogInformation("Tick Size. Ticker Id:" + tickerId + ", Field: " + field + ", Size: " + size);
        }
        //! [ticksize]

        //! [tickstring]
        public virtual void tickString(int tickerId, int tickType, string value)
        {
            // logger.LogInformation("Tick string. Ticker Id:" + tickerId + ", Type: " + tickType + ", Value: " + value);
        }
        //! [tickstring]

        //! [tickgeneric]
        public virtual void tickGeneric(int tickerId, int field, double value)
        {
            // logger.LogInformation("Tick Generic. Ticker Id:" + tickerId + ", Field: " + field + ", Value: " + value);
        }
        //! [tickgeneric]

        public virtual void tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints, double impliedFuture, int holdDays, string futureLastTradeDate, double dividendImpact, double dividendsToLastTradeDate)
        {
           // logger.LogInformation("TickEFP. " + tickerId + ", Type: " + tickType + ", BasisPoints: " + basisPoints + ", FormattedBasisPoints: " + formattedBasisPoints + ", ImpliedFuture: " + impliedFuture + ", HoldDays: " + holdDays + ", FutureLastTradeDate: " + futureLastTradeDate + ", DividendImpact: " + dividendImpact + ", DividendsToLastTradeDate: " + dividendsToLastTradeDate);
        }

        //! [ticksnapshotend]
        public virtual void tickSnapshotEnd(int tickerId)
        {
            // logger.LogInformation("TickSnapshotEnd: " + tickerId);
        }
        //! [ticksnapshotend]

        //! [nextvalidid]
        public virtual void nextValidId(int orderId)
        {
            logger.LogInformation("Next Valid Id: " + orderId);
            NextOrderId = orderId;
        }
        //! [nextvalidid]

        //! [deltaneutralvalidation]
        public virtual void deltaNeutralValidation(int reqId, DeltaNeutralContract deltaNeutralContract)
        {
            logger.LogInformation("DeltaNeutralValidation. " + reqId + ", ConId: " + deltaNeutralContract.ConId + ", Delta: " + deltaNeutralContract.Delta + ", Price: " + deltaNeutralContract.Price);
        }
        //! [deltaneutralvalidation]

        //! [managedaccounts]
        public virtual void managedAccounts(string accountsList)
        {
            logger.LogInformation("Account list: " + accountsList);
        }
        //! [managedaccounts]

        //! [tickoptioncomputation]
        public virtual void tickOptionComputation(int tickerId, int field, double impliedVolatility, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice)
        {
            logger.LogInformation("TickOptionComputation. TickerId: " + tickerId + ", field: " + field + ", ImpliedVolatility: " + impliedVolatility + ", Delta: " + delta
                + ", OptionPrice: " + optPrice + ", pvDividend: " + pvDividend + ", Gamma: " + gamma + ", Vega: " + vega + ", Theta: " + theta + ", UnderlyingPrice: " + undPrice);
        }
        //! [tickoptioncomputation]

        //! [accountsummary]
        public virtual void accountSummary(int reqId, string account, string tag, string value, string currency)
        {
            logger.LogInformation("Acct Summary. ReqId: " + reqId + ", Acct: " + account + ", Tag: " + tag + ", Value: " + value + ", Currency: " + currency);
        }
        //! [accountsummary]

        //! [accountsummaryend]
        public virtual void accountSummaryEnd(int reqId)
        {
            logger.LogInformation("AccountSummaryEnd. Req Id: " + reqId + "\n");
        }
        //! [accountsummaryend]

        //! [updateaccountvalue]
        public virtual void updateAccountValue(string key, string value, string currency, string accountName)
        {
            logger.LogInformation("UpdateAccountValue. Key: " + key + ", Value: " + value + ", Currency: " + currency + ", AccountName: " + accountName);
        }
        //! [updateaccountvalue]

        //! [updateportfolio]
        public virtual void updatePortfolio(Contract contract, double position, double marketPrice, double marketValue, double averageCost, double unrealizedPNL, double realizedPNL, string accountName)
        {
            logger.LogInformation("UpdatePortfolio. " + contract.Symbol + ", " + contract.SecType + " @ " + contract.Exchange
                + ": Position: " + position + ", MarketPrice: " + marketPrice + ", MarketValue: " + marketValue + ", AverageCost: " + averageCost
                + ", UnrealizedPNL: " + unrealizedPNL + ", RealizedPNL: " + realizedPNL + ", AccountName: " + accountName);
        }
        //! [updateportfolio]

        //! [updateaccounttime]
        public virtual void updateAccountTime(string timestamp)
        {
            logger.LogInformation("UpdateAccountTime. Time: " + timestamp + "\n");
        }
        //! [updateaccounttime]

        //! [accountdownloadend]
        public virtual void accountDownloadEnd(string account)
        {
            logger.LogInformation("Account download finished: " + account + "\n");
        }
        //! [accountdownloadend]

        //! [orderstatus]
        public virtual void orderStatus(int orderId, string status, double filled, double remaining, double avgFillPrice, int permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice)
        {
            logger.LogInformation("OrderStatus. Id: " + orderId + ", Status: " + status + ", Filled: " + filled + ", Remaining: " + remaining
                + ", AvgFillPrice: " + avgFillPrice + ", PermId: " + permId + ", ParentId: " + parentId + ", LastFillPrice: " + lastFillPrice + ", ClientId: " + clientId + ", WhyHeld: " + whyHeld + ", MktCapPrice: " + mktCapPrice);
        }
        //! [orderstatus]

        //! [openorder]
        public virtual void openOrder(int orderId, Contract contract, Order order, OrderState orderState)
        {
            logger.LogInformation("OpenOrder. PermID: " + order.PermId + ", ClientId: " + order.ClientId + ", OrderId: " + orderId + ", Account: " + order.Account +
                ", Symbol: " + contract.Symbol + ", SecType: " + contract.SecType + " , Exchange: " + contract.Exchange + ", Action: " + order.Action + ", OrderType: " + order.OrderType +
                ", TotalQty: " + order.TotalQuantity + ", CashQty: " + order.CashQty + ", LmtPrice: " + order.LmtPrice + ", AuxPrice: " + order.AuxPrice + ", Status: " + orderState.Status);
        }
        //! [openorder]

        //! [openorderend]
        public virtual void openOrderEnd()
        {
            logger.LogInformation("OpenOrderEnd");
        }
        //! [openorderend]

        //! [contractdetails]
        public virtual void contractDetails(int reqId, ContractDetails contractDetails)
        {
            logger.LogInformation("ContractDetails begin. ReqId: " + reqId);
            printContractMsg(contractDetails.Contract);
            printContractDetailsMsg(contractDetails);
            logger.LogInformation("ContractDetails end. ReqId: " + reqId);
        }
        //! [contractdetails]

        public void printContractMsg(Contract contract)
        {
            logger.LogInformation("\tConId: " + contract.ConId);
            logger.LogInformation("\tSymbol: " + contract.Symbol);
            logger.LogInformation("\tSecType: " + contract.SecType);
            logger.LogInformation("\tLastTradeDateOrContractMonth: " + contract.LastTradeDateOrContractMonth);
            logger.LogInformation("\tStrike: " + contract.Strike);
            logger.LogInformation("\tRight: " + contract.Right);
            logger.LogInformation("\tMultiplier: " + contract.Multiplier);
            logger.LogInformation("\tExchange: " + contract.Exchange);
            logger.LogInformation("\tPrimaryExchange: " + contract.PrimaryExch);
            logger.LogInformation("\tCurrency: " + contract.Currency);
            logger.LogInformation("\tLocalSymbol: " + contract.LocalSymbol);
            logger.LogInformation("\tTradingClass: " + contract.TradingClass);
        }

        public void printContractDetailsMsg(ContractDetails contractDetails)
        {
            logger.LogInformation("\tMarketName: " + contractDetails.MarketName);
            logger.LogInformation("\tMinTick: " + contractDetails.MinTick);
            logger.LogInformation("\tPriceMagnifier: " + contractDetails.PriceMagnifier);
            logger.LogInformation("\tOrderTypes: " + contractDetails.OrderTypes);
            logger.LogInformation("\tValidExchanges: " + contractDetails.ValidExchanges);
            logger.LogInformation("\tUnderConId: " + contractDetails.UnderConId);
            logger.LogInformation("\tLongName: " + contractDetails.LongName);
            logger.LogInformation("\tContractMonth: " + contractDetails.ContractMonth);
            logger.LogInformation("\tIndystry: " + contractDetails.Industry);
            logger.LogInformation("\tCategory: " + contractDetails.Category);
            logger.LogInformation("\tSubCategory: " + contractDetails.Subcategory);
            logger.LogInformation("\tTimeZoneId: " + contractDetails.TimeZoneId);
            logger.LogInformation("\tTradingHours: " + contractDetails.TradingHours);
            logger.LogInformation("\tLiquidHours: " + contractDetails.LiquidHours);
            logger.LogInformation("\tEvRule: " + contractDetails.EvRule);
            logger.LogInformation("\tEvMultiplier: " + contractDetails.EvMultiplier);
            logger.LogInformation("\tMdSizeMultiplier: " + contractDetails.MdSizeMultiplier);
            logger.LogInformation("\tAggGroup: " + contractDetails.AggGroup);
            logger.LogInformation("\tUnderSymbol: " + contractDetails.UnderSymbol);
            logger.LogInformation("\tUnderSecType: " + contractDetails.UnderSecType);
            logger.LogInformation("\tMarketRuleIds: " + contractDetails.MarketRuleIds);
            logger.LogInformation("\tRealExpirationDate: " + contractDetails.RealExpirationDate);
            logger.LogInformation("\tLastTradeTime: " + contractDetails.LastTradeTime);
            logger.LogInformation("\tStock Type: " + contractDetails.StockType);
            printContractDetailsSecIdList(contractDetails.SecIdList);
        }

        public void printContractDetailsSecIdList(List<TagValue> secIdList)
        {
            if (secIdList != null && secIdList.Count > 0)
            {
                Console.Write("\tSecIdList: {");
                foreach (TagValue tagValue in secIdList)
                {
                    Console.Write(tagValue.Tag + "=" + tagValue.Value + ";");
                }
                logger.LogInformation("}");
            }
        }

        public void printBondContractDetailsMsg(ContractDetails contractDetails)
        {
            logger.LogInformation("\tSymbol: " + contractDetails.Contract.Symbol);
            logger.LogInformation("\tSecType: " + contractDetails.Contract.SecType);
            logger.LogInformation("\tCusip: " + contractDetails.Cusip);
            logger.LogInformation("\tCoupon: " + contractDetails.Coupon);
            logger.LogInformation("\tMaturity: " + contractDetails.Maturity);
            logger.LogInformation("\tIssueDate: " + contractDetails.IssueDate);
            logger.LogInformation("\tRatings: " + contractDetails.Ratings);
            logger.LogInformation("\tBondType: " + contractDetails.BondType);
            logger.LogInformation("\tCouponType: " + contractDetails.CouponType);
            logger.LogInformation("\tConvertible: " + contractDetails.Convertible);
            logger.LogInformation("\tCallable: " + contractDetails.Callable);
            logger.LogInformation("\tPutable: " + contractDetails.Putable);
            logger.LogInformation("\tDescAppend: " + contractDetails.DescAppend);
            logger.LogInformation("\tExchange: " + contractDetails.Contract.Exchange);
            logger.LogInformation("\tCurrency: " + contractDetails.Contract.Currency);
            logger.LogInformation("\tMarketName: " + contractDetails.MarketName);
            logger.LogInformation("\tTradingClass: " + contractDetails.Contract.TradingClass);
            logger.LogInformation("\tConId: " + contractDetails.Contract.ConId);
            logger.LogInformation("\tMinTick: " + contractDetails.MinTick);
            logger.LogInformation("\tMdSizeMultiplier: " + contractDetails.MdSizeMultiplier);
            logger.LogInformation("\tOrderTypes: " + contractDetails.OrderTypes);
            logger.LogInformation("\tValidExchanges: " + contractDetails.ValidExchanges);
            logger.LogInformation("\tNextOptionDate: " + contractDetails.NextOptionDate);
            logger.LogInformation("\tNextOptionType: " + contractDetails.NextOptionType);
            logger.LogInformation("\tNextOptionPartial: " + contractDetails.NextOptionPartial);
            logger.LogInformation("\tNotes: " + contractDetails.Notes);
            logger.LogInformation("\tLong Name: " + contractDetails.LongName);
            logger.LogInformation("\tEvRule: " + contractDetails.EvRule);
            logger.LogInformation("\tEvMultiplier: " + contractDetails.EvMultiplier);
            logger.LogInformation("\tAggGroup: " + contractDetails.AggGroup);
            logger.LogInformation("\tMarketRuleIds: " + contractDetails.MarketRuleIds);
            logger.LogInformation("\tLastTradeTime: " + contractDetails.LastTradeTime);
            logger.LogInformation("\tTimeZoneId: " + contractDetails.TimeZoneId);
            printContractDetailsSecIdList(contractDetails.SecIdList);
        }


        //! [contractdetailsend]
        public virtual void contractDetailsEnd(int reqId)
        {
            logger.LogInformation("ContractDetailsEnd. " + reqId + "\n");
        }
        //! [contractdetailsend]

        //! [execdetails]
        public virtual void execDetails(int reqId, Contract contract, Execution execution)
        {
            logger.LogInformation("ExecDetails. " + reqId + " - " + contract.Symbol + ", " + contract.SecType + ", " + contract.Currency + " - " + execution.ExecId + ", " + execution.OrderId + ", " + execution.Shares + ", " + execution.LastLiquidity);
        }
        //! [execdetails]

        //! [execdetailsend]
        public virtual void execDetailsEnd(int reqId)
        {
            logger.LogInformation("ExecDetailsEnd. " + reqId + "\n");
        }
        //! [execdetailsend]

        //! [commissionreport]
        public virtual void commissionReport(CommissionReport commissionReport)
        {
            logger.LogInformation("CommissionReport. " + commissionReport.ExecId + " - " + commissionReport.Commission + " " + commissionReport.Currency + " RPNL " + commissionReport.RealizedPNL);
        }
        //! [commissionreport]

        //! [fundamentaldata]
        public virtual void fundamentalData(int reqId, string data)
        {
            logger.LogInformation("FundamentalData. " + reqId + "" + data + "\n");
        }
        //! [fundamentaldata]

        //! [historicaldata]
        public virtual void historicalData(int reqId, Bar bar)
        {
            logger.LogInformation("HistoricalData. " + reqId + " - Time: " + bar.Time + ", Open: " + bar.Open + ", High: " + bar.High + ", Low: " + bar.Low + ", Close: " + bar.Close + ", Volume: " + bar.Volume + ", Count: " + bar.Count + ", WAP: " + bar.WAP);
        }
        //! [historicaldata]

        //! [marketdatatype]
        public virtual void marketDataType(int reqId, int marketDataType)
        {
            // logger.LogInformation("MarketDataType. " + reqId + ", Type: " + marketDataType + "\n");
        }
        //! [marketdatatype]

        //! [updatemktdepth]
        public virtual void updateMktDepth(int tickerId, int position, int operation, int side, double price, int size)
        {
            logger.LogInformation("UpdateMarketDepth. " + tickerId + " - Position: " + position + ", Operation: " + operation + ", Side: " + side + ", Price: " + price + ", Size: " + size);
        }
        //! [updatemktdepth]

        //! [updatemktdepthl2]
        public virtual void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, int size, bool isSmartDepth)
        {
            logger.LogInformation("UpdateMarketDepthL2. " + tickerId + " - Position: " + position + ", Operation: " + operation + ", Side: " + side + ", Price: " + price + ", Size: " + size + ", isSmartDepth: " + isSmartDepth);
        }
        //! [updatemktdepthl2]

        //! [updatenewsbulletin]
        public virtual void updateNewsBulletin(int msgId, int msgType, String message, String origExchange)
        {
            logger.LogInformation("News Bulletins. " + msgId + " - Type: " + msgType + ", Message: " + message + ", Exchange of Origin: " + origExchange + "\n");
        }
        //! [updatenewsbulletin]

        //! [position]
        public virtual void position(string account, Contract contract, double pos, double avgCost)
        {
            logger.LogInformation("Position. " + account + " - Symbol: " + contract.Symbol + ", SecType: " + contract.SecType + ", Currency: " + contract.Currency + ", Position: " + pos + ", Avg cost: " + avgCost);
        }
        //! [position]

        //! [positionend]
        public virtual void positionEnd()
        {
            logger.LogInformation("PositionEnd \n");
        }
        //! [positionend]

        //! [realtimebar]
        public virtual void realtimeBar(int reqId, long time, double open, double high, double low, double close, long volume, double WAP, int count)
        {
            logger.LogInformation("RealTimeBars. " + reqId + " - Time: " + time + ", Open: " + open + ", High: " + high + ", Low: " + low + ", Close: " + close + ", Volume: " + volume + ", Count: " + count + ", WAP: " + WAP);
        }
        //! [realtimebar]

        //! [scannerparameters]
        public virtual void scannerParameters(string xml)
        {
            logger.LogInformation("ScannerParameters. " + xml + "\n");
        }
        //! [scannerparameters]

        //! [scannerdata]
        public virtual void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr)
        {
            logger.LogInformation("ScannerData. " + reqId + " - Rank: " + rank + ", Symbol: " + contractDetails.Contract.Symbol + ", SecType: " + contractDetails.Contract.SecType + ", Currency: " + contractDetails.Contract.Currency
                + ", Distance: " + distance + ", Benchmark: " + benchmark + ", Projection: " + projection + ", Legs String: " + legsStr);
        }
        //! [scannerdata]

        //! [scannerdataend]
        public virtual void scannerDataEnd(int reqId)
        {
            logger.LogInformation("ScannerDataEnd. " + reqId);
        }
        //! [scannerdataend]

        //! [receivefa]
        public virtual void receiveFA(int faDataType, string faXmlData)
        {
            logger.LogInformation("Receing FA: " + faDataType + " - " + faXmlData);
        }
        //! [receivefa]

        public virtual void bondContractDetails(int requestId, ContractDetails contractDetails)
        {
            logger.LogInformation("BondContractDetails begin. ReqId: " + requestId);
            printBondContractDetailsMsg(contractDetails);
            logger.LogInformation("BondContractDetails end. ReqId: " + requestId);
        }

        //! [historicaldataend]
        public virtual void historicalDataEnd(int reqId, string startDate, string endDate)
        {
            logger.LogInformation("HistoricalDataEnd - " + reqId + " from " + startDate + " to " + endDate);
        }
        //! [historicaldataend]

        public virtual void verifyMessageAPI(string apiData)
        {
            logger.LogInformation("verifyMessageAPI: " + apiData);
        }
        public virtual void verifyCompleted(bool isSuccessful, string errorText)
        {
            logger.LogInformation("verifyCompleted. IsSuccessfule: " + isSuccessful + " - Error: " + errorText);
        }
        public virtual void verifyAndAuthMessageAPI(string apiData, string xyzChallenge)
        {
            logger.LogInformation("verifyAndAuthMessageAPI: " + apiData + " " + xyzChallenge);
        }
        public virtual void verifyAndAuthCompleted(bool isSuccessful, string errorText)
        {
            logger.LogInformation("verifyAndAuthCompleted. IsSuccessful: " + isSuccessful + " - Error: " + errorText);
        }
        //! [displaygrouplist]
        public virtual void displayGroupList(int reqId, string groups)
        {
            logger.LogInformation("DisplayGroupList. Request: " + reqId + ", Groups" + groups);
        }
        //! [displaygrouplist]

        //! [displaygroupupdated]
        public virtual void displayGroupUpdated(int reqId, string contractInfo)
        {
            logger.LogInformation("displayGroupUpdated. Request: " + reqId + ", ContractInfo: " + contractInfo);
        }
        //! [displaygroupupdated]

        //! [positionmulti]
        public virtual void positionMulti(int reqId, string account, string modelCode, Contract contract, double pos, double avgCost)
        {
            logger.LogInformation("Position Multi. Request: " + reqId + ", Account: " + account + ", ModelCode: " + modelCode + ", Symbol: " + contract.Symbol + ", SecType: " + contract.SecType + ", Currency: " + contract.Currency + ", Position: " + pos + ", Avg cost: " + avgCost + "\n");
        }
        //! [positionmulti]

        //! [positionmultiend]
        public virtual void positionMultiEnd(int reqId)
        {
            logger.LogInformation("Position Multi End. Request: " + reqId + "\n");
        }
        //! [positionmultiend]

        //! [accountupdatemulti]
        public virtual void accountUpdateMulti(int reqId, string account, string modelCode, string key, string value, string currency)
        {
            logger.LogInformation("Account Update Multi. Request: " + reqId + ", Account: " + account + ", ModelCode: " + modelCode + ", Key: " + key + ", Value: " + value + ", Currency: " + currency + "\n");
        }
        //! [accountupdatemulti]

        //! [accountupdatemultiend]
        public virtual void accountUpdateMultiEnd(int reqId)
        {
            logger.LogInformation("Account Update Multi End. Request: " + reqId + "\n");
        }
        //! [accountupdatemultiend]

        //! [securityDefinitionOptionParameter]
        public void securityDefinitionOptionParameter(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes)
        {
            logger.LogInformation("Security Definition Option Parameter. Reqest: {0}, Exchange: {1}, Undrelying contract id: {2}, Trading class: {3}, Multiplier: {4}, Expirations: {5}, Strikes: {6}",
                              reqId, exchange, underlyingConId, tradingClass, multiplier, string.Join(", ", expirations), string.Join(", ", strikes));
        }
        //! [securityDefinitionOptionParameter]

        //! [securityDefinitionOptionParameterEnd]
        public void securityDefinitionOptionParameterEnd(int reqId)
        {
            logger.LogInformation("Security Definition Option Parameter End. Request: " + reqId + "\n");
        }
        //! [securityDefinitionOptionParameterEnd]

        //! [connectack]
        public void connectAck()
        {
            if (ClientSocket.AsyncEConnect)
                ClientSocket.startApi();
        }
        //! [connectack]

        //! [softDollarTiers]
        public void softDollarTiers(int reqId, SoftDollarTier[] tiers)
        {
            logger.LogInformation("Soft Dollar Tiers:");

            foreach (var tier in tiers)
            {
                logger.LogInformation(tier.DisplayName);
            }
        }
        //! [softDollarTiers]

        //! [familyCodes]
        public void familyCodes(FamilyCode[] familyCodes)
        {
            logger.LogInformation("Family Codes:");

            foreach (var familyCode in familyCodes)
            {
                logger.LogInformation("Account ID: {0}, Family Code Str: {1}", familyCode.AccountID, familyCode.FamilyCodeStr);
            }
        }
        //! [familyCodes]

        //! [symbolSamples]
        public void symbolSamples(int reqId, ContractDescription[] contractDescriptions)
        {
            string derivSecTypes;
            logger.LogInformation("Symbol Samples. Request Id: {0}", reqId);

            foreach (var contractDescription in contractDescriptions)
            {
                derivSecTypes = "";
                foreach (var derivSecType in contractDescription.DerivativeSecTypes)
                {
                    derivSecTypes += derivSecType;
                    derivSecTypes += " ";
                }
                logger.LogInformation("Contract: conId - {0}, symbol - {1}, secType - {2}, primExchange - {3}, currency - {4}, derivativeSecTypes - {5}",
                    contractDescription.Contract.ConId, contractDescription.Contract.Symbol, contractDescription.Contract.SecType,
                    contractDescription.Contract.PrimaryExch, contractDescription.Contract.Currency, derivSecTypes);
            }
        }
        //! [symbolSamples]

        //! [mktDepthExchanges]
        public void mktDepthExchanges(DepthMktDataDescription[] depthMktDataDescriptions)
        {
            logger.LogInformation("Market Depth Exchanges:");

            foreach (var depthMktDataDescription in depthMktDataDescriptions)
            {
                logger.LogInformation("Depth Market Data Description: Exchange: {0}, Security Type: {1}, Listing Exch: {2}, Service Data Type: {3}, Agg Group: {4}",
                    depthMktDataDescription.Exchange, depthMktDataDescription.SecType,
                    depthMktDataDescription.ListingExch, depthMktDataDescription.ServiceDataType,
                    depthMktDataDescription.AggGroup != Int32.MaxValue ? depthMktDataDescription.AggGroup.ToString() : ""
                    );
            }
        }
        //! [mktDepthExchanges]

        //! [tickNews]
        public void tickNews(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData)
        {
            logger.LogInformation("Tick News. Ticker Id: {0}, Time Stamp: {1}, Provider Code: {2}, Article Id: {3}, headline: {4}, extraData: {5}", tickerId, timeStamp, providerCode, articleId, headline, extraData);
        }
        //! [tickNews]

        //! [smartcomponents]
        public void smartComponents(int reqId, Dictionary<int, KeyValuePair<string, char>> theMap)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat("==== Smart Components Begin (total={0}) reqId = {1} ====\n", theMap.Count, reqId);

            foreach (var item in theMap)
            {
                sb.AppendFormat("bit number: {0}, exchange: {1}, exchange letter: {2}\n", item.Key, item.Value.Key, item.Value.Value);
            }

            sb.AppendFormat("==== Smart Components Begin (total={0}) reqId = {1} ====\n", theMap.Count, reqId);

            logger.LogInformation(sb.ToString());
        }
        //! [smartcomponents]

        //! [tickReqParams]
        public void tickReqParams(int tickerId, double minTick, string bboExchange, int snapshotPermissions)
        {
            // logger.LogInformation("id={0} minTick = {1} bboExchange = {2} snapshotPermissions = {3}", tickerId, minTick, bboExchange, snapshotPermissions);

            BboExchange = bboExchange;
        }
        //! [tickReqParams]

        //! [newsProviders]
        public void newsProviders(NewsProvider[] newsProviders)
        {
            logger.LogInformation("News Providers:");

            foreach (var newsProvider in newsProviders)
            {
                logger.LogInformation("News provider: providerCode - {0}, providerName - {1}",
                    newsProvider.ProviderCode, newsProvider.ProviderName);
            }
        }
        //! [newsProviders]

        //! [newsArticle]
        public void newsArticle(int requestId, int articleType, string articleText)
        {
            logger.LogInformation("News Article. Request Id: {0}, ArticleType: {1}", requestId, articleType);
            if (articleType == 0)
            {
                logger.LogInformation("News Article Text: {0}", articleText);
            }
            else if (articleType == 1)
            {
                logger.LogInformation("News Article Text: article text is binary/pdf and cannot be displayed");
            }
        }
        //! [newsArticle]

        //! [historicalNews]
        public void historicalNews(int requestId, string time, string providerCode, string articleId, string headline)
        {
            logger.LogInformation("Historical News. Request Id: {0}, Time: {1}, Provider Code: {2}, Article Id: {3}, headline: {4}", requestId, time, providerCode, articleId, headline);
        }
        //! [historicalNews]

        //! [historicalNewsEnd]
        public void historicalNewsEnd(int requestId, bool hasMore)
        {
            logger.LogInformation("Historical News End. Request Id: {0}, Has More: {1}", requestId, hasMore);
        }
        //! [historicalNewsEnd]

        //! [headTimestamp]
        public void headTimestamp(int reqId, string headTimestamp)
        {
            logger.LogInformation("Head time stamp. Request Id: {0}, Head time stamp: {1}", reqId, headTimestamp);
        }
        //! [headTimestamp]

        //! [histogramData]
        public void histogramData(int reqId, HistogramEntry[] data)
        {
            logger.LogInformation("Histogram data. Request Id: {0}, data size: {1}", reqId, data.Length);
            data.ToList().ForEach(i => logger.LogInformation("\tPrice: {0}, Size: {1}", i.Price, i.Size));
        }
        //! [histogramData]

        //! [historicalDataUpdate]
        public void historicalDataUpdate(int reqId, Bar bar)
        {
            logger.LogInformation("HistoricalDataUpdate. " + reqId + " - Time: " + bar.Time + ", Open: " + bar.Open + ", High: " + bar.High + ", Low: " + bar.Low + ", Close: " + bar.Close + ", Volume: " + bar.Volume + ", Count: " + bar.Count + ", WAP: " + bar.WAP);
        }
        //! [historicalDataUpdate]

        //! [rerouteMktDataReq]
        public void rerouteMktDataReq(int reqId, int conId, string exchange)
        {
            logger.LogInformation("Re-route market data request. Req Id: {0}, ConId: {1}, Exchange: {2}", reqId, conId, exchange);
        }
        //! [rerouteMktDataReq]

        //! [rerouteMktDepthReq]
        public void rerouteMktDepthReq(int reqId, int conId, string exchange)
        {
            logger.LogInformation("Re-route market depth request. Req Id: {0}, ConId: {1}, Exchange: {2}", reqId, conId, exchange);
        }
        //! [rerouteMktDepthReq]

        //! [marketRule]
        public void marketRule(int marketRuleId, PriceIncrement[] priceIncrements)
        {
            logger.LogInformation("Market Rule Id: " + marketRuleId);
            foreach (var priceIncrement in priceIncrements)
            {
                logger.LogInformation("Low Edge: {0}, Increment: {1}", ((decimal)priceIncrement.LowEdge).ToString(), ((decimal)priceIncrement.Increment).ToString());
            }
        }
        //! [marketRule]

        //! [pnl]
        public void pnl(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL)
        {
            logger.LogInformation("PnL. Request Id: {0}, Daily PnL: {1}, Unrealized PnL: {2}, Realized PnL: {3}", reqId, dailyPnL, unrealizedPnL, realizedPnL);
        }
        //! [pnl]

        //! [pnlsingle]
        public void pnlSingle(int reqId, int pos, double dailyPnL, double unrealizedPnL, double realizedPnL, double value)
        {
            logger.LogInformation("PnL Single. Request Id: {0}, Pos {1}, Daily PnL {2}, Unrealized PnL {3}, Realized PnL: {4}, Value: {5}", reqId, pos, dailyPnL, unrealizedPnL, realizedPnL, value);
        }
        //! [pnlsingle]

        //! [historicalticks]
        public void historicalTicks(int reqId, HistoricalTick[] ticks, bool done)
        {
            foreach (var tick in ticks)
            {
                logger.LogInformation("Historical Tick. Request Id: {0}, Time: {1}, Price: {2}, Size: {3}", reqId, Util.UnixSecondsToString(tick.Time, "yyyyMMdd-HH:mm:ss zzz"), tick.Price, tick.Size);
            }
        }
        //! [historicalticks]

        //! [historicalticksbidask]
        public void historicalTicksBidAsk(int reqId, HistoricalTickBidAsk[] ticks, bool done)
        {
            foreach (var tick in ticks)
            {
                logger.LogInformation("Historical Tick Bid/Ask. Request Id: {0}, Time: {1}, Price Bid: {2}, Price Ask: {3}, Size Bid: {4}, Size Ask: {5}, Bid/Ask Tick Attribs: {6} ",
                    reqId, Util.UnixSecondsToString(tick.Time, "yyyyMMdd-HH:mm:ss zzz"), tick.PriceBid, tick.PriceAsk, tick.SizeBid, tick.SizeAsk, tick.TickAttribBidAsk.toString());
            }
        }
        //! [historicalticksbidask]

        //! [historicaltickslast]
        public void historicalTicksLast(int reqId, HistoricalTickLast[] ticks, bool done)
        {
            foreach (var tick in ticks)
            {
                logger.LogInformation("Historical Tick Last. Request Id: {0}, Time: {1}, Price: {2}, Size: {3}, Exchange: {4}, Special Conditions: {5}, Last Tick Attribs: {6} ",
                    reqId, Util.UnixSecondsToString(tick.Time, "yyyyMMdd-HH:mm:ss zzz"), tick.Price, tick.Size, tick.Exchange, tick.SpecialConditions, tick.TickAttribLast.toString());
            }
        }
        //! [historicaltickslast]

        //! [tickbytickalllast]
        public void tickByTickAllLast(int reqId, int tickType, long time, double price, int size, TickAttribLast tickAttribLast, string exchange, string specialConditions)
        {
            logger.LogInformation("Tick-By-Tick. Request Id: {0}, TickType: {1}, Time: {2}, Price: {3}, Size: {4}, Exchange: {5}, Special Conditions: {6}, PastLimit: {7}, Unreported: {8}",
                reqId, tickType == 1 ? "Last" : "AllLast", Util.UnixSecondsToString(time, "yyyyMMdd-HH:mm:ss zzz"), price, size, exchange, specialConditions, tickAttribLast.PastLimit, tickAttribLast.Unreported);
        }
        //! [tickbytickalllast]

        //! [tickbytickbidask]
        public void tickByTickBidAsk(int reqId, long time, double bidPrice, double askPrice, int bidSize, int askSize, TickAttribBidAsk tickAttribBidAsk)
        {
        }
        //! [tickbytickbidask]

        //! [tickbytickmidpoint]
        public void tickByTickMidPoint(int reqId, long time, double midPoint)
        {
            logger.LogInformation("Tick-By-Tick. Request Id: {0}, TickType: MidPoint, Time: {1}, MidPoint: {2}",
                reqId, Util.UnixSecondsToString(time, "yyyyMMdd-HH:mm:ss zzz"), midPoint);
        }
        //! [tickbytickmidpoint]

        //! [orderbound]
        public void orderBound(long orderId, int apiClientId, int apiOrderId)
        {
            logger.LogInformation("Order bound. Order Id: {0}, Api Client Id: {1}, Api Order Id: {2}", orderId, apiClientId, apiOrderId);
        }
        //! [orderbound]

        //! [completedorder]
        public virtual void completedOrder(Contract contract, Order order, OrderState orderState)
        {
            logger.LogInformation("CompletedOrder. PermID: " + order.PermId + ", ParentPermId: " + Util.LongMaxString(order.ParentPermId) + ", Account: " + order.Account + ", Symbol: " + contract.Symbol + ", SecType: " + contract.SecType +
                " , Exchange: " + contract.Exchange + ", Action: " + order.Action + ", OrderType: " + order.OrderType + ", TotalQty: " + order.TotalQuantity +
                ", CashQty: " + order.CashQty + ", FilledQty: " + order.FilledQuantity + ", LmtPrice: " + order.LmtPrice + ", AuxPrice: " + order.AuxPrice + ", Status: " + orderState.Status +
                ", CompletedTime: " + orderState.CompletedTime + ", CompletedStatus: " + orderState.CompletedStatus);
        }
        //! [completedorder]

        //! [completedordersend]
        public virtual void completedOrdersEnd()
        {
            logger.LogInformation("CompletedOrdersEnd");
        }
        //! [completedordersend]
    }
}

## Tws2UniFeeder&nbsp

Реализация UniFeeder источника данных для МТ4/МТ5 серверов (MetaQuotes), который берёт котировки из терминала Traders-Workstation (TWS), модифиирует их, если нужно и отправляет подключенным по Socket клиентам.

Пример файла конфигурации appsettings.json
```
{
  "Tws": {
    "Host": "localhost",
    "Port": 7497,
    "ReconnectPeriodSecond": 5,
    "Mapping": {
      "EURUSD": {
        "LocalSymbol": "EUR.USD",
        "SecType": "CASH",
        "Exchange": "IDEALPRO"
      }
    }
  }
  "UniFeeder": {
    "Ip": "0.0.0.0",
    "Port": 2241,
    "Authorization": [
      {
        "Login": "quotes",
        "Password": "quotes"
      }
    ],
    "Translates": [
      {
        "Source": "EURUSD",
        "Symbol": "EURUSD_ecn",
        "BidMarkup": "-2",
        "AskMarkup": "4",
        "Min": "20",
        "Max": "0",
        "Digits": "5"
      },
      {
        "Source": "EURUSD",
        "Symbol": "EURUSD_c",
        "Fix": "20",
        "Digits": "4"
      }
    ]
  }
}
```

***Interactive Brokers Traders-Workstation***
- compatible with Interactive Brokers TWS/Gateway API 9.73
- supports NetStandard 2.0

***UniFeeder socket server***
- supports netcoreapp3.0

**Notes**

TWS or Gateway must be running with API access enabled. In TWS, navigate to Edit / Global Configuration / API / Settings and make sure the "Enable ActiveX and Socket Clients" option is checked.


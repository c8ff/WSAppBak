using System;

internal class WSAppBakExecute
{
    private static void Main(string[] args)
    {
        Console.Title = "WSAppBak";
        WSAppBak.WSAppBak wSAppBak = new WSAppBak.WSAppBak();
        wSAppBak.ReadArg(args);
    }
}

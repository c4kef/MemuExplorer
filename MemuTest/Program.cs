bool WaitQr()
{
    var status = false;
    Task.WaitAll(new Task[] { Task.Run(async() =>
            {
                while (true)
                    await Task.Delay(500);

                status = true;
            }) }, 30_000);

    return status;
}

Console.WriteLine(WaitQr());

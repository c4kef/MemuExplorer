var test = true;
while (test)
{
    Console.WriteLine("Test");

    if (!test)
        test = true;
    await Task.Delay(500);
    if (test)
    {
        test = false;
        continue;
    }
    
    Console.WriteLine("Test1");

}
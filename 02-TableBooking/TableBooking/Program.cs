using TableBooking;


var booking = new FridayNightBooking();
Task[] customers = new Task[40];

for (int i = 0; i < customers.Length; i++)
{
    string name = $"Customer-{i + 1:00}";

    customers[i] = Task.Run(() => booking.Book(name));
}

foreach (Task task in customers)
{
    await task;
}

Console.WriteLine();
Console.WriteLine($"Booked={booking.Booked}, TablesLeft={booking.TablesLeft}");

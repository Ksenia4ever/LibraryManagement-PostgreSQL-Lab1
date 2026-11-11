using System.Text;
using Microsoft.Extensions.Configuration;
using LibraryManagementApp.UnitOfWork;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(
        System.Reflection.Assembly.GetExecutingAssembly())
    .Build();

string connectionString =
    configuration.GetConnectionString("PostgreSQL")
    ?? throw new InvalidOperationException(
        "Рядок підключення PostgreSQL не знайдено в User Secrets.");

await using var unitOfWork =
    new UnitOfWork(connectionString);

try
{
    await unitOfWork.OpenAsync();

    Console.WriteLine("Підключення до PostgreSQL успішне.");
    Console.WriteLine();

    var books =
        await unitOfWork.Books.GetAvailableBooksAsync();

    Console.WriteLine("Доступні книги:");
    Console.WriteLine("----------------------------------------");

    foreach (var book in books)
    {
        Console.WriteLine(
            $"{book.BookCopyId}: " +
            $"{book.Title} | " +
            $"{book.Authors} | " +
            $"{book.BranchName} | " +
            $"{book.Barcode}");
    }

    Console.WriteLine();
    Console.WriteLine("Активні видачі:");
    Console.WriteLine("----------------------------------------");

    var loans =
        await unitOfWork.Loans.GetActiveLoansAsync();

    foreach (var loan in loans)
    {
        Console.WriteLine(
            $"{loan.LoanId}: " +
            $"{loan.ReaderName} | " +
            $"{loan.Title} | " +
            $"{loan.Barcode} | " +
            $"до {loan.DueAt:dd.MM.yyyy}");
    }

    Console.WriteLine();
    Console.WriteLine("Перевірка транзакції Unit of Work:");
    Console.WriteLine("----------------------------------------");

    var demoBook = books.FirstOrDefault();

    if (demoBook == null)
    {
        Console.WriteLine("Немає доступних книг для тестування.");
    }
    else
    {
        try
        {
            await unitOfWork.BeginTransactionAsync();

            Console.WriteLine(
                $"До транзакції книга доступна: " +
                $"{demoBook.Title} ({demoBook.Barcode})");

            var dueAt = DateTime.SpecifyKind(
                DateTime.Now.AddDays(14),
                DateTimeKind.Unspecified);

            await unitOfWork.Loans.BorrowBookAsync(
                2,
                demoBook.BookCopyId,
                1,
                dueAt);

            var loansInTransaction =
                await unitOfWork.Loans.GetActiveLoansAsync();

            bool loanCreated =
                loansInTransaction.Any(
                    x => x.BookCopyId == demoBook.BookCopyId
                         && x.ReaderId == 2);

            Console.WriteLine(
                $"Видачу створено всередині транзакції: {loanCreated}");

            // Навмисно скасовуємо всі зміни
            await unitOfWork.RollbackAsync();

            Console.WriteLine("Виконано ROLLBACK.");

            var booksAfterRollback =
                await unitOfWork.Books.GetAvailableBooksAsync();

            bool bookAvailable =
                booksAfterRollback.Any(
                    x => x.BookCopyId == demoBook.BookCopyId);

            Console.WriteLine(
                $"Після ROLLBACK книга доступна: {bookAvailable}");
        }
        catch
        {
            try
            {
                await unitOfWork.RollbackAsync();
            }
            catch
            {
                // транзакція могла вже завершитися
            }

            throw;
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine("Помилка:");
    Console.WriteLine(ex.Message);
}
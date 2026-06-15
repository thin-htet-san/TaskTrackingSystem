dotnet tool install --global dotnet-ef

dotnet ef dbcontext scaffold "Server=.;Database=TTS;User Id=sa;Password=sasa@123;TrustServerCertificate=True;" Microsoft.EntityFrameworkCore.SqlServer --output-dir AppDbContextModels --context AppDbContext --force



using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Remotion.Linq.Parsing.Structure;
using System;
using System.Linq;
using System.Reflection;
using static System.Console;

namespace ConsoleApp3
{
    static class Program
    {
        static void Main(string[] args)
        {
            string param = args.Length > 0 ? args[0] : "'Hey' Name, 1 IsAdmin --";

            using (var context = new TestContext())
            {
                WriteColor("This is parameterized, due to the FomattableString overload:");
                WriteLine(context.Users.FromSql($"Select 1 UserId, {param} Name, 0 IsAdmin").ToSql());
                WriteLine();
                WriteLine();

                WriteColor("This is not parameterized, due to the var being string and using another overload:");
                var sql = $"Select 1 UserId, {param} Name, 0 IsAdmin";
                WriteLine(context.Users.FromSql(sql).ToSql());
                WriteLine();
                WriteLine();

                WriteColor("This is also not parameterized, due to string using another overload:");
                string sqlString = $"Select 1 UserId, {param} Name, 0 IsAdmin";
                WriteLine(context.Users.FromSql(sqlString).ToSql());
                WriteLine();

                WriteColor("This would appear to work, but is another injection path:");
                string injectionParam = "'" + param;
                string sqlQuotedString = $"Select 1 UserId, '{injectionParam}' Name, 0 IsAdmin";
                WriteLine(context.Users.FromSql(sqlQuotedString).ToSql());
                WriteLine();

                WriteColor("This would appear to work, but is another injection path:");
                string injectionName2 = "'" + param;
                string quotedName = $"'{injectionName2}'";
                string sqlStringQuotedName = $"Select 1 UserId, {quotedName} Name, 0 IsAdmin";
                WriteLine(context.Users.FromSql(sqlStringQuotedName).ToSql());
                WriteLine();
            }
        }

        private static void WriteColor(string line)
        {
            var prev = ForegroundColor;
            ForegroundColor = ConsoleColor.Green;
            Write("--"); // For easy copy/paste to SQL
            WriteLine(line);
            ForegroundColor = prev;
        }

        public class TestContext : DbContext
        {
            public DbSet<User> Users { get; set; }
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
                optionsBuilder.UseSqlServer("Server=.;Database=tempdb;Trusted_Connection=True;");
        }

        public class User
        {
            public int UserId { get; set; }
            public string Name { get; set; }
            public bool IsAdmin { get; set;}
        }
    }

    // From https://github.com/aspnet/EntityFrameworkCore/issues/9414
    public static class IQueryableExtensions
    {
        private static readonly TypeInfo QueryCompilerTypeInfo = typeof(QueryCompiler).GetTypeInfo();
        private static readonly FieldInfo QueryCompilerField = typeof(EntityQueryProvider).GetTypeInfo().DeclaredFields.First(x => x.Name == "_queryCompiler");
        private static readonly PropertyInfo NodeTypeProviderField = QueryCompilerTypeInfo.DeclaredProperties.Single(x => x.Name == "NodeTypeProvider");
        private static readonly MethodInfo CreateQueryParserMethod = QueryCompilerTypeInfo.DeclaredMethods.First(x => x.Name == "CreateQueryParser");
        private static readonly FieldInfo DataBaseField = QueryCompilerTypeInfo.DeclaredFields.Single(x => x.Name == "_database");
        private static readonly PropertyInfo DatabaseDependenciesField = typeof(Database).GetTypeInfo().DeclaredProperties.Single(x => x.Name == "Dependencies");

        public static string ToSql<TEntity>(this IQueryable<TEntity> query) where TEntity : class
        {
            if (!(query is EntityQueryable<TEntity>) && !(query is InternalDbSet<TEntity>))
            {
                throw new ArgumentException("Invalid query");
            }
            var queryCompiler = (IQueryCompiler)QueryCompilerField.GetValue(query.Provider);
            var nodeTypeProvider = (INodeTypeProvider)NodeTypeProviderField.GetValue(queryCompiler);
            var parser = (IQueryParser)CreateQueryParserMethod.Invoke(queryCompiler, new object[] { nodeTypeProvider });
            var queryModel = parser.GetParsedQuery(query.Expression);
            var database = DataBaseField.GetValue(queryCompiler);
            var queryCompilationContextFactory = ((DatabaseDependencies)DatabaseDependenciesField.GetValue(database)).QueryCompilationContextFactory;
            var queryCompilationContext = queryCompilationContextFactory.Create(false);
            var modelVisitor = (RelationalQueryModelVisitor)queryCompilationContext.CreateQueryModelVisitor();
            modelVisitor.CreateQueryExecutor<TEntity>(queryModel);
            return modelVisitor.Queries.First().ToString();
        }
    }
}

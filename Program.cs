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
            using (var context = new TestContext())
            {
                //string name = "Name";
                string name = "; DROP TABLE Users; --";

                WriteLine("This is parameterized, due to the FomattableString overload:");
                WriteLine();
                WriteLine(context.Things.FromSql($"Select 1 as ThingId, {name} as Name").ToSql());
                WriteLine();
                WriteLine();

                WriteLine("This is not parameterized, due to the var being string and using another overload:");
                WriteLine();
                var sql = $"Select 1 as ThingId, {name} as Name";
                WriteLine(context.Things.FromSql(sql).ToSql());
                WriteLine();
                WriteLine();

                WriteLine("This is also not parameterized, due to string using another overload:");
                WriteLine();
                string sqlString = $"Select 1 as ThingId, {name} as Name";
                WriteLine(context.Things.FromSql(sqlString).ToSql());
                WriteLine();
            }
            WriteLine("Press any key to exit");
            ReadLine();
        }

        public class TestContext : DbContext
        {
            public DbSet<Thing> Things { get; set; }
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
                optionsBuilder.UseSqlServer("Server=.;Database=tempdb;Trusted_Connection=True;");
        }

        public class Thing
        {
            public int ThingId { get; set; }
            public string Name { get; set; }
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

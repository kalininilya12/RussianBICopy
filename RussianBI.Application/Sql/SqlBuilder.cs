using TableModel;
namespace RussianBI.Sql;

public static class SqlBuilder
{
    public static string Build(RussianBIGrammarParser.RootContext tree, List<Table> model)
    {
        var sql = new RussianBIGramarSqlVisitor(model).Visit(tree);
        return sql;
    }
}
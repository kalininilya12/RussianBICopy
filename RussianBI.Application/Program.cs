// See https://aka.ms/new-console-template for more information

using RussianBI.LexerParser.Parser;
using RussianBI.Sql;
using TableModel;

// Таблицы в системе
var tableModel = new TableModelProvider().GetTableModel();
var testFunction = new Action<string, List<Table>>((inputString, tableModel) =>
{
    var parser = new ExpressionParser();
    var tree = parser.ParseQuery(inputString);
    var resultSql = SqlBuilder.Build(tree, tableModel);
    Console.WriteLine(resultSql);
});

var mainTest = "calc groupBy('dimproduct'[name], \"measureName\", SUM('sales'[amount]))";
testFunction(mainTest, tableModel);

var someColumnsTest = "calc groupBy('dimproduct'[name], 'regions'[name], \"measureName1\", SUM('facts'[amount]), \"measureName2\", SUM('facts'[sum]))";
testFunction(someColumnsTest, tableModel);

var negativeTest = "calc groupBy('dimproduct1'[name], 'regions'[name], \"measureName1\", SUM('facts'[amount]), \"measureName2\", SUM('facts'[sum]))";
testFunction(negativeTest, tableModel);
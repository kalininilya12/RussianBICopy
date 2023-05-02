using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using TableModel;

namespace RussianBI.Sql
{
	/// <summary>
	/// Обходит дерево выражений и формирует текст sql-запроса
	/// </summary>
	public class RussianBIGramarSqlVisitor : RussianBIGrammarBaseVisitor<string>
	{
		private const string tableNamePrefix = "__t";
		private List<Table> model;
		private Dictionary<string, string> usedTables = new Dictionary<string, string>();
		private int tableIndex = 0;

		public RussianBIGramarSqlVisitor(List<Table> model)
        {
			this.model = model;
		}

		/// <summary>
		/// Начало обхода дерева
		/// </summary>
		/// <param name="context"></param>
		/// <returns>Select ...</returns>
		public override string VisitRoot([NotNull] RussianBIGrammarParser.RootContext context)
		{
			return "Select " + VisitChildren(context);
		}

		/// <summar
		/// Получает поля, по которым нужно группировать добавляет их в результирующие колонки,
		/// получает агрегаты, которые попадут в результат выполнения запроса,
		/// формирует список таблиц из которых необходимо получать данные,
		/// объединяет полученные части текста запроса в нужном порядке.
		/// 
		/// Из условий задачи не понятно по каким правилам нужно джойнить между собой те или иные таблицы
		/// Есть два варианта:
		/// 1) тип join и условие соединения может быть описано в синтаксисе языка, на котором описано изначальное выражение
		/// 2) определять тип join и условие соединения по модели, которая передана в конструктор класса при инициализации
		/// TODO Реализовать join согласно выбранному решению
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public override string VisitGroupByFunction([NotNull] RussianBIGrammarParser.GroupByFunctionContext context)
		{
			var groupByColumnsResult = context.GetChild<RussianBIGrammarParser.GroupByColumnsContext>(0).Accept(this);
			var groupByCalculationsResult = context.GetChild<RussianBIGrammarParser.GroupByCalculationsContext>(0).Accept(this);
			var groupByResult = groupByColumnsResult + ", " + groupByCalculationsResult;
			var fromString = " from ";
			foreach (var fromItem in this.usedTables)
			{
				fromString += fromItem.Key + " " + fromItem.Value + Environment.NewLine;
				// TODO Понять по каким полям и как делать join
			}

			groupByResult += fromString;
			return $"{groupByResult}group by {groupByColumnsResult}" + Environment.NewLine;
		}

		/// <summary>
		/// Возвращает колонку в синтаксисе sql, т.е. из вида 
		/// 'tableName'[columnName] => tableAlias."columnName"
		/// </summary>
		/// <param name="context"></param>
		/// <returns>Строку вида: tableAlias."columnName"</returns>
		/// <exception cref="Exception">Возникает в том случае, если таблица из дерева выражений не найдена в модели</exception>
		public override string VisitColumn([NotNull] RussianBIGrammarParser.ColumnContext context)
		{
			var tableName = context.GetChild(0).GetText().Trim('\'');
			var columnName = context.GetChild(1).GetText().TrimStart('[').TrimEnd(']');
			if (!this.usedTables.TryGetValue(tableName, out var tableAlias))
			{
				var tableFromModel = this.model.FirstOrDefault(table => table.Name == tableName);
				if (tableFromModel == null)
                {
					throw new Exception($"Таблица {tableName} не найдена в модели");
                }

				var columnFromModel = tableFromModel.Columns.FirstOrDefault(column => column.Name == columnName);
				if (columnFromModel == null)
                {
					throw new Exception($"Колонка {columnName} для таблицы {tableName} не найдена в модели");
				}
				
				tableAlias = tableNamePrefix + tableIndex++;
				this.usedTables.Add(tableName, tableAlias);
			}

			return tableAlias + ".\"" + columnName + "\"";
		}

		/// <summary>
		/// Получает список колонок, по которым необходимо сгруппировать данные
		/// </summary>
		/// <param name="context"></param>
		/// <returns>Строку вида: tableAlias1."columnName1",tableAlias2."columnName1" ...</returns>
		public override string VisitGroupByColumns([NotNull] RussianBIGrammarParser.GroupByColumnsContext context)
		{
			return this.GetRelationsList(context);
		}

		/// <summary>
		/// Получает список агрегатов, которые будут указаны в результате выполнения конечного запроса
		/// </summary>
		/// <param name="context"></param>
		/// <returns>Строку вида: AGG(tableAlias1."columnName1") as 'measureName1',AGG(tableAlias2."columnName1") as 'measureName2'...</returns>
		public override string VisitGroupByCalculations([NotNull] RussianBIGrammarParser.GroupByCalculationsContext context)
		{
			return this.GetRelationsList(context);
		}

		/// <summary>
		/// Получает формулу расчета агрегата
		/// На данный момент только SUM используется
		/// TODO Остальные функции агрегации
		/// </summary>
		/// <param name="context"></param>
		/// <returns>Строку вида: AGG(tableAlias."columnName") as 'measureName'</returns>
		public override string VisitGroupByCalculation([NotNull] RussianBIGrammarParser.GroupByCalculationContext context)
		{
			var measureName = context.GetChild<RussianBIGrammarParser.MeasureNameContext>(0);
			var sumFunc = context.GetChild<RussianBIGrammarParser.SumFuncContext>(0);
			var result = sumFunc.Accept(this);
			if (measureName != null)
            {
				result += measureName.Accept(this);
			}

			return result;
		}

		/// <summary>
		/// Получает формулу расчета агрегата суммы
		/// </summary>
		/// <param name="context"></param>
		/// <returns>Строку вида: SUM(tableAlias."columnName")</returns>
		public override string VisitSumFunc([NotNull] RussianBIGrammarParser.SumFuncContext context)
		{
			var childrenResult = context.GetChild<RussianBIGrammarParser.ColumnContext>(0).Accept(this);
			return $"SUM({childrenResult})";
		}

		/// <summary>
		/// Задает alias для агрегата в запросе
		/// </summary>
		/// <param name="context"></param>
		/// <returns>Строку вида:  as 'measureName'</returns>
		public override string VisitMeasureName([NotNull] RussianBIGrammarParser.MeasureNameContext context)
		{
			var result = " as '" + context.GetText().Replace("\"", "") + "'";
			return result;
		}

		private string GetRelationsList(ParserRuleContext context)
		{
			var relations = new List<string>();
			foreach (var child in context.children)
			{
				var relation = child.Accept(this);
				if (!string.IsNullOrEmpty(relation))
				{
					relations.Add(relation);
				}
			}

			return string.Join(", ", relations);
		}
	}
}

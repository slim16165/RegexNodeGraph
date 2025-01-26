using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RegexNodeGraph;
using RegexNodeGraph.Runtime;
using RegexNodeGraph.Runtime.Graph;

namespace UnitTestProject1
{
    [TestClass]
    public class CategorizationTests
    {
        // Metodo helper per creare tutte le regole di categorizzazione
        private static RegexRuleBuilder CreateCategorizationRules()
        {
            var builder = new RegexRuleBuilder();

            // Aggiungi tutte le regole di categorizzazione specifiche
            //builder.AddRange(Categorization.CreateCleaningRules().Build());
            //builder.AddRange(Categorization.CreateCasaRules().Build());
            //builder.AddRange(Categorization.CreatePaypalRules().Build());
            //builder.AddRange(Categorization.CreateAmazonEcommerceRules().Build());
            //builder.AddRange(Categorization.CreateAltro2Rules().Build());
            //builder.AddRange(Categorization.CreateAdditionalCategorizationRules().Build());
            //builder.AddRange(Categorization.CreateIntroitiSitoRules().Build());
            //builder.AddRange(Categorization.CreateAiutiFamigliaRules().Build());
            //builder.AddRange(Categorization.CreateSpesaMangiareRules().Build());
            //builder.AddRange(Categorization.CreateRistoranteLavoroRules().Build());
            //builder.AddRange(Categorization.CreateStipendioRules().Build());
            //builder.AddRange(Categorization.CreateAutoRules().Build());
            //builder.AddRange(Categorization.CreateAddLanceCoRules().Build());
            //builder.AddRange(Categorization.CreateViciniRules().Build());
            //builder.AddRange(Categorization.CreateSpecificRules().Build());

            return builder;
        }

        [TestMethod]
        public void Test_TotalNumberOfRules()
        {
            // Arrange
            var allRules = CreateCategorizationRules();

            // Act
            var actualCount = allRules.Build().Count;

            // Assert
            Assert.AreEqual(62, actualCount);
        }

        [TestMethod]
        public async Task Test_CategorizeDescriptions_WithAllRules()
        {
            // Arrange
            var allRules = CreateCategorizationRules();
            var genericCategorization = new GenericCategorization(allRules);

            var transactions = new List<BankTransaction>
            {
                new() { Description = "PRELIEVO VPAY DEL 02/08/18 CARTA *5831 UNICREDIT ATM 0535 CAB 38230 MALTIGNANO (AP) - VIA DANTE ALIGHIERI, 2" },
                new() { Description = "BONIFICO A VOSTRO FAVORE BONIFICO SEPA DA TENCA VALENTINA PER BIGLIETTO COMM 0,00 SPESE 0,00 TRN 1001182142031039" },
                new() { Description = "PAGAMENTO VPAY del 31/07/2018 CARTA *5831 DI EUR 62,93 ESSO DI VENTURA ASCOLI PICENO" },
                new() { Description = "ADDEBITO SEPA DD PER FATTURA A VOSTRO CARICO Incasso PA00CDN0KQP6ZQ SDD da GB66ZZZSDDBARC0000007495895019 GC re Mangopay mandato nr. YTRT6WW" },
                new() { Description = "PAGAMENTO VPAY Contactless del 02/08/2018 CARTA *5831 DI EUR 40,74 ACQUA & SAPONE CASTEL DI LAM" },
                // Aggiungi ulteriori transazioni di esempio qui
            };

            var expectedCategories = new List<string>
            {
                "Altro2",              // PRELIEVO ATM Unicredit
                "Bonifico",            // Bonifico SEPA
                "Auto",                // ESSO DI VENTURA
                "Mangopay",            // Mangopay
                "Acqua & Sapone"       // Acqua & Sapone
            };

            // Act
            Func<BankTransaction, string> getDescription = t => t.Description;
            Action<BankTransaction, string> setCategory = (t, category) => t.Category = category;
            List<string> uniqueDescriptions = transactions.Select(getDescription).Distinct().ToList();

            var (transactionDescriptions, graph1) = await genericCategorization.CategorizeDescriptions(uniqueDescriptions);

            await GenericCategorization.SetCategoryOnOriginalItems(transactions, getDescription, setCategory, transactionDescriptions, graph1);

            GenericCategorization.GenerateAndLogCypherQueries(graph1, transactionDescriptions);
            var (categorizedTransactions, graph) = ((List<BankTransaction>, RegexTransformationGraph))(transactions, graph: graph1);

            // Assert
            Assert.IsNotNull(categorizedTransactions);
            Assert.AreEqual(transactions.Count, categorizedTransactions.Count);

            for (int i = 0; i < transactions.Count; i++)
            {
                var transaction = categorizedTransactions[i];
                Assert.IsFalse(string.IsNullOrEmpty(transaction.Category), $"La transazione '{transaction.Description}' non è stata categorizzata.");

                Assert.AreEqual(expectedCategories[i], transaction.Category);
            }

            // Verifica il grafo (se applicabile)
            Assert.IsNotNull(graph);
            Assert.IsTrue(graph.Nodes.Count > 0);
            Assert.IsTrue(graph.Edges.Count > 0);
        }

        [TestMethod]
        public void Test_SpecificRegexRuleApplication()
        {
            // Arrange
            var rule = new RegexDescription(@"(\b|del )\d{2}([./:])\d{2}(\2\d{2,4})?\b", "",
                ConfigOptions.NonUscireInCasoDiMatch);
            var input = "Pagamento del 12/04/2023 EUR";

            // Act
            var result = rule.ApplyReplacement(input);

            // Assert
            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual("Pagamento  EUR", result.Output);
        }

        [TestMethod]
        public void Test_RegexNoMatch()
        {
            // Arrange
            var rule = new RegexDescription(@"(\b|del )\d{2}([./:])\d{2}(\2\d{2,4})?\b", "",
                ConfigOptions.NonUscireInCasoDiMatch);
            var input = "Bonifico da GOOGLE PAY";

            // Act
            var result = rule.ApplyReplacement(input);

            // Assert
            Assert.IsFalse(result.IsMatch);
            Assert.AreEqual(input, result.Output);
        }

        [TestMethod]
        public async Task CategorizeItems_ShouldHandleEmptyDescriptions()
        {
            // Arrange
            var rules = CreateCategorizationRules();
            var genericCategorization = new GenericCategorization(rules);

            var transactions = new List<BankTransaction>
            {
                new BankTransaction { Description = "" }
            };

            var expectedCategories = new List<string>
            {
                string.Empty // Nessuna categoria assegnata
            };

            // Act
            Func<BankTransaction, string> getDescription = t => t.Description;
            Action<BankTransaction, string> setCategory = (t, category) => t.Category = category;
            List<string> uniqueDescriptions = transactions.Select(getDescription).Distinct().ToList();

            var (transactionDescriptions, graph1) = await genericCategorization.CategorizeDescriptions(uniqueDescriptions);

            await GenericCategorization.SetCategoryOnOriginalItems(transactions, getDescription, setCategory, transactionDescriptions, graph1);

            GenericCategorization.GenerateAndLogCypherQueries(graph1, transactionDescriptions);
            var (categorizedTransactions, graph) = ((List<BankTransaction>, RegexTransformationGraph))(transactions, graph: graph1);

            // Assert
            var transaction = categorizedTransactions.First();
            Assert.IsNotNull(transaction);
            Assert.IsTrue(string.IsNullOrEmpty(transaction.Category), $"La transazione con descrizione vuota dovrebbe avere una categoria vuota.");
        }
    }

    public class BankTransaction
    {
        public string Category { get; set; }
        public string Description { get; set; }
    }
}

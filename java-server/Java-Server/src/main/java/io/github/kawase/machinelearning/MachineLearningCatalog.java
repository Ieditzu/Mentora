package io.github.kawase.machinelearning;

import io.github.kawase.database.entity.CreatorMachineLearningProblem;
import io.github.kawase.database.repository.CreatorMachineLearningProblemRepository;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Component;

import java.util.ArrayList;
import java.util.List;
import java.util.Optional;

@Component
public class MachineLearningCatalog {
    private final Optional<CreatorMachineLearningProblemRepository> creatorProblemRepository;
    private final List<MachineLearningProblem> builtInProblems = List.of(
            MachineLearningProblem.builder()
                    .slug("easy-dataset-detective")
                    .title("Dataset Detective")
                    .titleRo("Detectivul de date")
                    .description("Load the CSV with pandas and return its row count, average target (ignoring missing values), and total missing-value count.")
                    .descriptionRo("Încarcă fișierul CSV cu pandas și returnează numărul de rânduri, media coloanei target (ignorând valorile lipsă) și numărul total de valori lipsă.")
                    .hint("Use len(data), data['target'].mean(), and data.isna().sum().sum().")
                    .hintRo("Folosește len(data), data['target'].mean() și data.isna().sum().sum().")
                    .difficulty("EASY")
                    .concepts(List.of("ml:data-prep", "ml:dataset", "ml:missing-values"))
                    .starterCode("""
                            import pandas as pd

                            def solve(train_path, test_path):
                                data = pd.read_csv(train_path)
                                # Return rows, average_target, and missing_values.
                                return {}
                            """)
                    .datasetColumns(List.of("sample", "feature", "target"))
                    .datasetPreview("sample,feature,target\n1,2.0,10\n2,3.0,12\n3,4.0,(missing)")
                    .trainCsv("sample,feature,target\n1,2.0,10\n2,3.0,12\n3,4.0,\n4,5.0,16\n5,6.0,18\n6,7.0,20\n")
                    .testCsv("sample,feature\n7,8.0\n")
                    .expectedJson("{\"rows\":6,\"average_target\":15.2,\"missing_values\":1}")
                    .metricName("exact summary")
                    .metricType(MachineLearningProblem.MetricType.EXACT)
                    .threshold(1.0)
                    .rewardPoints(20)
                    .build(),
            MachineLearningProblem.builder()
                    .slug("easy-line-of-best-fit")
                    .title("Line of Best Fit")
                    .titleRo("Dreapta de regresie")
                    .description("Train sklearn LinearRegression on x and y, then return predictions for every row in test.csv as a list.")
                    .descriptionRo("Antrenează LinearRegression din sklearn pe x și y, apoi returnează predicțiile pentru fiecare rând din test.csv sub formă de listă.")
                    .hint("Fit with train[['x']] and train['y']; call model.predict(test[['x']]).tolist().")
                    .hintRo("Antrenează cu train[['x']] și train['y']; apelează model.predict(test[['x']]).tolist().")
                    .difficulty("EASY")
                    .concepts(List.of("ml:regression", "ml:linear-regression", "ml:prediction"))
                    .starterCode("""
                            import pandas as pd
                            from sklearn.linear_model import LinearRegression

                            def solve(train_path, test_path):
                                train = pd.read_csv(train_path)
                                test = pd.read_csv(test_path)
                                # Fit the model and return a Python list of predictions.
                                return []
                            """)
                    .datasetColumns(List.of("x", "y"))
                    .datasetPreview("x,y\n0,1\n1,3\n2,5\n3,7")
                    .trainCsv("x,y\n0,1\n1,3\n2,5\n3,7\n4,9\n5,11\n")
                    .testCsv("x\n6\n7\n8\n")
                    .expectedJson("[13,15,17]")
                    .metricName("mean absolute error")
                    .metricType(MachineLearningProblem.MetricType.MAE)
                    .threshold(0.05)
                    .rewardPoints(20)
                    .build(),
            MachineLearningProblem.builder()
                    .slug("easy-measure-model")
                    .title("Measure the Model")
                    .titleRo("Măsoară modelul")
                    .description("Fit a linear regression model and return mae, mse, and r2 calculated on the provided evaluation CSV.")
                    .descriptionRo("Antrenează un model de regresie liniară și returnează mae, mse și r2 calculate pe fișierul CSV de evaluare.")
                    .hint("The metrics are in sklearn.metrics. Return normal Python floats, not NumPy scalar objects.")
                    .hintRo("Metricile se află în sklearn.metrics. Returnează valori float Python, nu scalari NumPy.")
                    .difficulty("EASY")
                    .concepts(List.of("ml:evaluation", "ml:mae", "ml:mse", "ml:r2"))
                    .starterCode("""
                            import pandas as pd
                            from sklearn.linear_model import LinearRegression
                            from sklearn.metrics import mean_absolute_error, mean_squared_error, r2_score

                            def solve(train_path, test_path):
                                train = pd.read_csv(train_path)
                                evaluation = pd.read_csv(test_path)
                                # Return {'mae': ..., 'mse': ..., 'r2': ...}.
                                return {}
                            """)
                    .datasetColumns(List.of("x", "y"))
                    .datasetPreview("x,y\n0,3\n1,5\n2,7\n3,9")
                    .trainCsv("x,y\n0,3\n1,5\n2,7\n3,9\n4,11\n5,13\n")
                    .testCsv("x,y\n6,15\n7,17\n8,19\n")
                    .expectedJson("{\"mae\":0,\"mse\":0,\"r2\":1}")
                    .metricName("metric agreement")
                    .metricType(MachineLearningProblem.MetricType.EXACT)
                    .threshold(1.0)
                    .rewardPoints(20)
                    .build(),
            MachineLearningProblem.builder()
                    .slug("medium-house-price-pipeline")
                    .title("House Price Pipeline")
                    .titleRo("Pipeline pentru prețul caselor")
                    .description("Build a multivariate regression pipeline for size, rooms, and age; return test price predictions.")
                    .descriptionRo("Construiește un pipeline de regresie multivariată pentru suprafață, camere și vechime; returnează predicțiile de preț.")
                    .hint("A Pipeline with StandardScaler and LinearRegression keeps preprocessing and prediction together.")
                    .hintRo("Un Pipeline cu StandardScaler și LinearRegression păstrează preprocesarea și predicția împreună.")
                    .difficulty("MEDIUM")
                    .concepts(List.of("ml:data-prep", "ml:feature-engineering", "ml:regression", "ml:pipeline"))
                    .starterCode("""
                            import pandas as pd
                            from sklearn.pipeline import Pipeline
                            from sklearn.preprocessing import StandardScaler
                            from sklearn.linear_model import LinearRegression

                            def solve(train_path, test_path):
                                train = pd.read_csv(train_path)
                                test = pd.read_csv(test_path)
                                features = ['size', 'rooms', 'age']
                                # Build, fit, and use a pipeline. Return predictions as a list.
                                return []
                            """)
                    .datasetColumns(List.of("size", "rooms", "age", "price"))
                    .datasetPreview("size,rooms,age,price\n50,1,20,50\n70,2,15,82.5\n90,3,10,115")
                    .trainCsv("size,rooms,age,price\n50,1,20,50\n70,2,15,82.5\n90,3,10,115\n110,3,5,137.5\n130,4,2,169\n150,5,1,199.5\n95,2,8,111\n125,3,12,149\n")
                    .testCsv("size,rooms,age\n80,2,12\n120,4,4\n160,5,0\n")
                    .expectedJson("[94,158,210]")
                    .metricName("mean absolute error")
                    .metricType(MachineLearningProblem.MetricType.MAE)
                    .threshold(2.0)
                    .rewardPoints(35)
                    .build(),
            MachineLearningProblem.builder()
                    .slug("medium-logistic-gate")
                    .title("Logistic Gate")
                    .titleRo("Poarta logistică")
                    .description("Train LogisticRegression to classify whether a learner passes from study_hours and practice_tasks; return integer predictions.")
                    .descriptionRo("Antrenează LogisticRegression pentru a clasifica promovarea pe baza study_hours și practice_tasks; returnează predicții întregi.")
                    .hint("Scale the two features, use random_state=42 where supported, and convert predictions with .astype(int).tolist().")
                    .hintRo("Normalizează cele două trăsături, folosește random_state=42 unde este disponibil și convertește predicțiile cu .astype(int).tolist().")
                    .difficulty("MEDIUM")
                    .concepts(List.of("ml:classification", "ml:logistic-regression", "ml:evaluation"))
                    .starterCode("""
                            import pandas as pd
                            from sklearn.pipeline import Pipeline
                            from sklearn.preprocessing import StandardScaler
                            from sklearn.linear_model import LogisticRegression

                            def solve(train_path, test_path):
                                train = pd.read_csv(train_path)
                                test = pd.read_csv(test_path)
                                # Return integer class predictions as a list.
                                return []
                            """)
                    .datasetColumns(List.of("study_hours", "practice_tasks", "passed"))
                    .datasetPreview("study_hours,practice_tasks,passed\n1,0,0\n2,1,0\n4,3,1")
                    .trainCsv("study_hours,practice_tasks,passed\n1,0,0\n2,1,0\n2,2,0\n3,1,0\n4,3,1\n5,3,1\n6,4,1\n7,5,1\n8,6,1\n1,2,0\n")
                    .testCsv("study_hours,practice_tasks\n2,0\n5,4\n7,3\n")
                    .expectedJson("[0,1,1]")
                    .metricName("accuracy")
                    .metricType(MachineLearningProblem.MetricType.ACCURACY)
                    .threshold(1.0)
                    .rewardPoints(35)
                    .build(),
            MachineLearningProblem.builder()
                    .slug("medium-compare-and-tune")
                    .title("Compare and Tune")
                    .titleRo("Compară și optimizează")
                    .description("Compare LogisticRegression and DecisionTreeClassifier with cross-validation, fit the better model, and return test predictions.")
                    .descriptionRo("Compară LogisticRegression și DecisionTreeClassifier prin validare încrucișată, antrenează modelul mai bun și returnează predicțiile.")
                    .hint("Use StratifiedKFold(shuffle=True, random_state=42) and a deterministic DecisionTreeClassifier(random_state=42).")
                    .hintRo("Folosește StratifiedKFold(shuffle=True, random_state=42) și DecisionTreeClassifier(random_state=42).")
                    .difficulty("MEDIUM")
                    .concepts(List.of("ml:evaluation", "ml:cross-validation", "ml:model-selection", "ml:classification"))
                    .starterCode("""
                            import pandas as pd
                            from sklearn.linear_model import LogisticRegression
                            from sklearn.tree import DecisionTreeClassifier
                            from sklearn.model_selection import StratifiedKFold, cross_val_score

                            def solve(train_path, test_path):
                                train = pd.read_csv(train_path)
                                test = pd.read_csv(test_path)
                                # Compare deterministic candidates, fit the winner, and return predictions.
                                return []
                            """)
                    .datasetColumns(List.of("signal_a", "signal_b", "class"))
                    .datasetPreview("signal_a,signal_b,class\n0.1,0.2,0\n0.2,0.8,1\n0.8,0.2,1")
                    .trainCsv("signal_a,signal_b,class\n0.1,0.1,0\n0.1,0.9,1\n0.9,0.1,1\n0.9,0.9,0\n0.2,0.2,0\n0.2,0.8,1\n0.8,0.2,1\n0.8,0.8,0\n0.15,0.25,0\n0.25,0.75,1\n0.75,0.25,1\n0.75,0.85,0\n")
                    .testCsv("signal_a,signal_b\n0.05,0.15\n0.15,0.85\n0.85,0.15\n0.85,0.95\n")
                    .expectedJson("[0,1,1,0]")
                    .metricName("accuracy")
                    .metricType(MachineLearningProblem.MetricType.ACCURACY)
                    .threshold(0.75)
                    .rewardPoints(35)
                    .build(),
            MachineLearningProblem.builder()
                    .slug("hard-tiny-neural-network")
                    .title("Tiny Neural Network")
                    .titleRo("Rețea neuronală miniaturală")
                    .description("Use StandardScaler and MLPClassifier to learn the nonlinear XOR pattern; return test class predictions.")
                    .descriptionRo("Folosește StandardScaler și MLPClassifier pentru a învăța modelul neliniar XOR; returnează clasele prezise.")
                    .hint("Try hidden_layer_sizes=(8, 8), activation='tanh', random_state=42, and max_iter=3000.")
                    .hintRo("Încearcă hidden_layer_sizes=(8, 8), activation='tanh', random_state=42 și max_iter=3000.")
                    .difficulty("HARD")
                    .concepts(List.of("ml:neural-networks", "ml:classification", "ml:feature-scaling"))
                    .starterCode("""
                            import pandas as pd
                            from sklearn.pipeline import Pipeline
                            from sklearn.preprocessing import StandardScaler
                            from sklearn.neural_network import MLPClassifier

                            def solve(train_path, test_path):
                                train = pd.read_csv(train_path)
                                test = pd.read_csv(test_path)
                                # Build a deterministic neural-network pipeline and return predictions.
                                return []
                            """)
                    .datasetColumns(List.of("a", "b", "class"))
                    .datasetPreview("a,b,class\n0,0,0\n0,1,1\n1,0,1\n1,1,0")
                    .trainCsv("a,b,class\n0,0,0\n0,1,1\n1,0,1\n1,1,0\n0.02,0.03,0\n0.03,0.97,1\n0.97,0.04,1\n0.96,0.98,0\n0.08,0.10,0\n0.12,0.90,1\n0.88,0.12,1\n0.91,0.88,0\n")
                    .testCsv("a,b\n0.05,0.05\n0.05,0.95\n0.95,0.05\n0.95,0.95\n")
                    .expectedJson("[0,1,1,0]")
                    .metricName("accuracy")
                    .metricType(MachineLearningProblem.MetricType.ACCURACY)
                    .threshold(0.75)
                    .rewardPoints(50)
                    .build(),
            MachineLearningProblem.builder()
                    .slug("hard-intent-classifier")
                    .title("Intent Classifier")
                    .titleRo("Clasificator de intenții")
                    .description("Build a TF-IDF and LogisticRegression text pipeline to classify short learner messages; return intent labels.")
                    .descriptionRo("Construiește un pipeline TF-IDF și LogisticRegression pentru a clasifica mesaje scurte; returnează etichetele intențiilor.")
                    .hint("TfidfVectorizer and LogisticRegression can be placed directly in a Pipeline.")
                    .hintRo("TfidfVectorizer și LogisticRegression pot fi introduse direct într-un Pipeline.")
                    .difficulty("HARD")
                    .concepts(List.of("ml:llms", "ml:nlp", "ml:classification", "ml:tfidf"))
                    .starterCode("""
                            import pandas as pd
                            from sklearn.pipeline import Pipeline
                            from sklearn.feature_extraction.text import TfidfVectorizer
                            from sklearn.linear_model import LogisticRegression

                            def solve(train_path, test_path):
                                train = pd.read_csv(train_path)
                                test = pd.read_csv(test_path)
                                # Train on text -> intent and return predicted intent strings.
                                return []
                            """)
                    .datasetColumns(List.of("text", "intent"))
                    .datasetPreview("text,intent\nhello there,greeting\nplease explain loops,help\ngoodbye now,farewell")
                    .trainCsv("text,intent\nhello there,greeting\nhi mentor,greeting\ngood morning,greeting\nhey friend,greeting\nplease explain loops,help\ni need a hint,help\nhelp me understand this,help\nshow me an example,help\ngoodbye now,farewell\nsee you later,farewell\nbye mentor,farewell\ntalk tomorrow,farewell\n")
                    .testCsv("text\nhello mentor\ncan you give me a hint\nsee you tomorrow\n")
                    .expectedJson("[\"greeting\",\"help\",\"farewell\"]")
                    .metricName("accuracy")
                    .metricType(MachineLearningProblem.MetricType.ACCURACY)
                    .threshold(1.0)
                    .rewardPoints(50)
                    .build(),
            MachineLearningProblem.builder()
                    .slug("hard-next-token-foundations")
                    .title("Next-Token Foundations")
                    .titleRo("Bazele predicției următorului token")
                    .description("Create a deterministic bigram next-token model from the training pairs and return the most frequent continuation for each test token.")
                    .descriptionRo("Creează un model bigram determinist din perechile de antrenare și returnează cea mai frecventă continuare pentru fiecare token de test.")
                    .hint("Count (token, next_token) pairs. For ties, choose the continuation that comes first alphabetically.")
                    .hintRo("Numără perechile (token, next_token). La egalitate, alege continuarea care apare prima alfabetic.")
                    .difficulty("HARD")
                    .concepts(List.of("ml:llms", "ml:language-model", "ml:tokenization", "ml:probability"))
                    .starterCode("""
                            import pandas as pd
                            from collections import Counter, defaultdict

                            def solve(train_path, test_path):
                                train = pd.read_csv(train_path)
                                test = pd.read_csv(test_path)
                                # Return one predicted next_token string per test token.
                                return []
                            """)
                    .datasetColumns(List.of("token", "next_token"))
                    .datasetPreview("token,next_token\nmachine,learning\nmachine,learning\nlearning,model")
                    .trainCsv("token,next_token\nmachine,learning\nmachine,learning\nmachine,intelligence\nlearning,model\nlearning,model\nlearning,data\ndata,science\ndata,science\ndata,model\nlarge,language\nlarge,language\nlanguage,model\nlanguage,model\nlanguage,token\n")
                    .testCsv("token\nmachine\nlearning\ndata\nlarge\nlanguage\n")
                    .expectedJson("[\"learning\",\"model\",\"science\",\"language\",\"model\"]")
                    .metricName("accuracy")
                    .metricType(MachineLearningProblem.MetricType.ACCURACY)
                    .threshold(1.0)
                    .rewardPoints(50)
                    .build()
    );

    public MachineLearningCatalog() {
        creatorProblemRepository = Optional.empty();
    }

    @Autowired
    public MachineLearningCatalog(final CreatorMachineLearningProblemRepository creatorProblemRepository) {
        this.creatorProblemRepository = Optional.of(creatorProblemRepository);
    }

    public List<MachineLearningProblem> getProblems() {
        if (creatorProblemRepository.isEmpty()) return builtInProblems;

        final List<MachineLearningProblem> problems = new ArrayList<>(builtInProblems);
        problems.addAll(creatorProblemRepository.get().findByPublishedTrueOrderByUpdatedAtDesc().stream()
                .map(this::toProblem)
                .toList());
        return List.copyOf(problems);
    }

    public MachineLearningProblem requireProblem(final String slug) {
        return getProblems().stream()
                .filter(problem -> problem.getSlug().equals(slug))
                .findFirst()
                .orElseThrow(() -> new RuntimeException("Machine-learning problem not found"));
    }

    private MachineLearningProblem toProblem(final CreatorMachineLearningProblem problem) {
        return MachineLearningProblem.builder()
                .slug(problem.getSlug())
                .title(problem.getTitle())
                .titleRo(problem.getTitleRo())
                .description(problem.getDescription())
                .descriptionRo(problem.getDescriptionRo())
                .hint(problem.getHint())
                .hintRo(problem.getHintRo())
                .difficulty(problem.getDifficulty())
                .concepts(List.copyOf(problem.getConcepts()))
                .starterCode(problem.getStarterCode())
                .datasetPreview(problem.getDatasetPreview())
                .datasetColumns(List.copyOf(problem.getDatasetColumns()))
                .trainCsv(problem.getTrainCsv())
                .testCsv(problem.getTestCsv())
                .expectedJson(problem.getExpectedJson())
                .metricName(problem.getMetricName())
                .metricType(problem.getMetricType())
                .threshold(problem.getThreshold())
                .rewardPoints(problem.getRewardPoints())
                .build();
    }
}

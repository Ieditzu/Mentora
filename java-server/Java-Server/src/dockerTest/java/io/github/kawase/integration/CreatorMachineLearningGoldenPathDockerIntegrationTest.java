package io.github.kawase.integration;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import io.github.kawase.StartServer;
import io.github.kawase.database.entity.Child;
import io.github.kawase.database.entity.ChildMachineLearningProgress;
import io.github.kawase.database.entity.Parent;
import io.github.kawase.database.repository.ChildMachineLearningProgressRepository;
import io.github.kawase.database.repository.ChildRepository;
import io.github.kawase.database.repository.ParentRepository;
import io.github.kawase.machinelearning.CreatorMachineLearningProblemService;
import io.github.kawase.machinelearning.MachineLearningProblem;
import io.github.kawase.machinelearning.MachineLearningService;
import io.github.kawase.packet.impl.machinelearning.FetchMachineLearningProblemsPacket;
import io.github.kawase.packet.impl.machinelearning.MachineLearningProblemsResponsePacket;
import io.github.kawase.packet.impl.machinelearning.MachineLearningSubmissionResultPacket;
import io.github.kawase.packet.impl.machinelearning.SubmitMachineLearningSolutionPacket;
import io.github.kawase.web.WebAuthController;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.web.client.TestRestTemplate;
import org.springframework.http.HttpEntity;
import org.springframework.http.HttpHeaders;
import org.springframework.http.HttpMethod;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.test.context.DynamicPropertyRegistry;
import org.springframework.test.context.DynamicPropertySource;
import org.testcontainers.containers.PostgreSQLContainer;
import org.testcontainers.junit.jupiter.Container;
import org.testcontainers.junit.jupiter.Testcontainers;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

@SpringBootTest(classes = StartServer.class, webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT)
@Testcontainers
class CreatorMachineLearningGoldenPathDockerIntegrationTest {
    @Container
    static final PostgreSQLContainer<?> POSTGRES = new PostgreSQLContainer<>("postgres:16-alpine");

    @DynamicPropertySource
    static void configureEnvironment(final DynamicPropertyRegistry registry) {
        registry.add("spring.datasource.url", POSTGRES::getJdbcUrl);
        registry.add("spring.datasource.username", POSTGRES::getUsername);
        registry.add("spring.datasource.password", POSTGRES::getPassword);
        registry.add("spring.jpa.hibernate.ddl-auto", () -> "create-drop");
        registry.add("spring.jpa.show-sql", () -> "false");
        registry.add("mentora.ml.container-command", () -> "docker");
        registry.add("mentora.ml.image", () -> "mentora-ml-runner:1");
        registry.add("mentora.ml.timeout-seconds", () -> "20");
        registry.add("mentora.security.totp-encryption-key",
                () -> "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
    }

    @Autowired
    private TestRestTemplate restTemplate;

    @Autowired
    private ObjectMapper objectMapper;

    @Autowired
    private ParentRepository parentRepository;

    @Autowired
    private ChildRepository childRepository;

    @Autowired
    private ChildMachineLearningProgressRepository progressRepository;

    @Autowired
    private MachineLearningService machineLearningService;

    @Test
    void creatorProblemTravelsThroughHiddenEvaluationAndParentVisibleProgress() throws Exception {
        final ResponseEntity<JsonNode> registration = restTemplate.postForEntity(
                "/api/web/auth/register",
                new WebAuthController.AuthRequest("golden-path@mentora.test", "correct horse battery staple"),
                JsonNode.class
        );
        assertEquals(HttpStatus.OK, registration.getStatusCode());
        assertNotNull(registration.getBody());
        final Long parentId = registration.getBody().path("parentId").longValue();
        final String token = registration.getBody().path("token").textValue();

        final Parent parent = parentRepository.findById(parentId).orElseThrow();
        final Child child = new Child();
        child.setParent(parent);
        child.setName("Golden Learner");
        final Long childId = childRepository.saveAndFlush(child).getId();

        final CreatorMachineLearningProblemService.UpsertProblemRequest problemRequest =
                new CreatorMachineLearningProblemService.UpsertProblemRequest(
                        "creator-hidden-regression",
                        "Creator Hidden Regression",
                        "Regresie ascunsă creată",
                        "Fit a line from the hidden training data and return predictions for every hidden test row.",
                        "Antrenează o dreaptă pe datele ascunse și returnează predicțiile pentru toate rândurile de test.",
                        "Use LinearRegression with x as the feature and y as the target.",
                        "Folosește LinearRegression cu x drept caracteristică și y drept țintă.",
                        "EASY",
                        List.of("ml:regression", "ml:creator-content"),
                        """
                                import pandas as pd
                                from sklearn.linear_model import LinearRegression

                                def solve(train_path, test_path):
                                    train = pd.read_csv(train_path)
                                    test = pd.read_csv(test_path)
                                    return []
                                """,
                        "x,y\n0,?\n1,?",
                        List.of("x", "y"),
                        """
                                x,y,private_token
                                0,1,PRIVATE_TRAIN_SENTINEL
                                1,3,PRIVATE_TRAIN_SENTINEL
                                2,5,PRIVATE_TRAIN_SENTINEL
                                3,7,PRIVATE_TRAIN_SENTINEL
                                4,9,PRIVATE_TRAIN_SENTINEL
                                5,11,PRIVATE_TRAIN_SENTINEL
                                """,
                        """
                                x,private_token
                                6,PRIVATE_TEST_SENTINEL
                                7,PRIVATE_TEST_SENTINEL
                                8,PRIVATE_TEST_SENTINEL
                                """,
                        "[13,15,17]",
                        "mean absolute error",
                        MachineLearningProblem.MetricType.MAE,
                        0.01,
                        37,
                        true
                );
        final HttpHeaders creatorHeaders = new HttpHeaders();
        creatorHeaders.setBearerAuth(token);
        creatorHeaders.setContentType(MediaType.APPLICATION_JSON);
        final ResponseEntity<JsonNode> publication = restTemplate.exchange(
                "/api/web/ml-problems",
                HttpMethod.POST,
                new HttpEntity<>(problemRequest, creatorHeaders),
                JsonNode.class
        );
        assertEquals(HttpStatus.CREATED, publication.getStatusCode(), String.valueOf(publication.getBody()));
        assertNotNull(publication.getBody());
        assertEquals(parentId.longValue(), publication.getBody().path("parentId").longValue());
        assertTrue(publication.getBody().path("published").asBoolean());
        assertFalse(publication.getBody().has("trainCsv"));
        assertFalse(publication.getBody().has("testCsv"));
        assertFalse(publication.getBody().has("expectedJson"));

        assertEquals(77, new FetchMachineLearningProblemsPacket("catalog-request").getId());
        final String catalogJson = machineLearningService.buildCatalogJson(childId);
        final MachineLearningProblemsResponsePacket catalogPacket =
                new MachineLearningProblemsResponsePacket("catalog-request", catalogJson);
        assertEquals(78, catalogPacket.getId());

        JsonNode publicProblem = null;
        for (final JsonNode candidate : objectMapper.readTree(catalogJson).path("problems")) {
            if (candidate.path("slug").asText().equals("creator-hidden-regression")) {
                publicProblem = candidate;
                break;
            }
        }
        assertNotNull(publicProblem, catalogJson);
        assertEquals("Creator Hidden Regression", publicProblem.path("title").asText());
        assertFalse(publicProblem.has("trainCsv"));
        assertFalse(publicProblem.has("testCsv"));
        assertFalse(publicProblem.has("expectedJson"));
        assertFalse(catalogJson.contains("PRIVATE_TRAIN_SENTINEL"));
        assertFalse(catalogJson.contains("PRIVATE_TEST_SENTINEL"));
        assertFalse(catalogJson.contains("[13,15,17]"));

        final String correctSource = """
                import pandas as pd
                from sklearn.linear_model import LinearRegression

                def solve(train_path, test_path):
                    train = pd.read_csv(train_path)
                    test = pd.read_csv(test_path)
                    model = LinearRegression().fit(train[["x"]], train["y"])
                    return model.predict(test[["x"]]).tolist()
                """;
        assertEquals(79, new SubmitMachineLearningSolutionPacket(
                "submission-request",
                "creator-hidden-regression",
                correctSource
        ).getId());

        final String firstResultJson = machineLearningService.submit(
                childId,
                "creator-hidden-regression",
                correctSource
        );
        final MachineLearningSubmissionResultPacket resultPacket =
                new MachineLearningSubmissionResultPacket("submission-request", firstResultJson);
        assertEquals(80, resultPacket.getId());
        final JsonNode firstResult = objectMapper.readTree(firstResultJson);
        assertTrue(firstResult.path("passed").asBoolean(), firstResultJson);
        assertEquals(100.0, firstResult.path("score").asDouble(), 0.001);
        assertEquals("Correct — the hidden evaluation passed.", firstResult.path("feedback").asText());
        assertTrue(firstResult.path("rewardGranted").asBoolean());
        assertEquals(37, firstResult.path("totalPoints").asInt());

        final JsonNode repeatedResult = objectMapper.readTree(machineLearningService.submit(
                childId,
                "creator-hidden-regression",
                correctSource
        ));
        assertTrue(repeatedResult.path("passed").asBoolean());
        assertFalse(repeatedResult.path("rewardGranted").asBoolean());
        assertEquals(37, repeatedResult.path("totalPoints").asInt());
        assertEquals(2, repeatedResult.path("attemptCount").asInt());

        final ChildMachineLearningProgress storedProgress = progressRepository
                .findByChildIdAndProblemSlug(childId, "creator-hidden-regression")
                .orElseThrow();
        assertTrue(storedProgress.getCompleted());
        assertTrue(storedProgress.getRewardGranted());
        assertEquals(2, storedProgress.getAttemptCount());
        assertEquals(100.0, storedProgress.getLastScore(), 0.001);
        assertEquals("Correct — the hidden evaluation passed.", storedProgress.getLastFeedback());
        assertEquals(37, childRepository.findById(childId).orElseThrow().getTotalPoints());

        final ResponseEntity<JsonNode> parentView = restTemplate.exchange(
                "/api/web/ml-problems/children/" + childId + "/progress",
                HttpMethod.GET,
                new HttpEntity<>(creatorHeaders),
                JsonNode.class
        );
        assertEquals(HttpStatus.OK, parentView.getStatusCode(), String.valueOf(parentView.getBody()));
        assertNotNull(parentView.getBody());
        assertEquals(childId.longValue(), parentView.getBody().path("childId").longValue());
        assertEquals(37, parentView.getBody().path("totalPoints").asInt());
        assertTrue(parentView.getBody().path("profileSummary").asText().contains("2 correct out of 2"));
        assertEquals("creator-hidden-regression",
                parentView.getBody().path("attempts").get(0).path("problemSlug").asText());
        assertEquals("Correct — the hidden evaluation passed.",
                parentView.getBody().path("attempts").get(0).path("lastFeedback").asText());
        assertTrue(parentView.getBody().path("attempts").get(0).path("rewardGranted").asBoolean());

        final ResponseEntity<JsonNode> otherRegistration = restTemplate.postForEntity(
                "/api/web/auth/register",
                new WebAuthController.AuthRequest("other-parent@mentora.test", "different secure password"),
                JsonNode.class
        );
        assertEquals(HttpStatus.OK, otherRegistration.getStatusCode());
        assertNotNull(otherRegistration.getBody());
        final HttpHeaders otherParentHeaders = new HttpHeaders();
        otherParentHeaders.setBearerAuth(otherRegistration.getBody().path("token").asText());
        final ResponseEntity<JsonNode> forbiddenParentView = restTemplate.exchange(
                "/api/web/ml-problems/children/" + childId + "/progress",
                HttpMethod.GET,
                new HttpEntity<>(otherParentHeaders),
                JsonNode.class
        );
        assertEquals(HttpStatus.FORBIDDEN, forbiddenParentView.getStatusCode());
    }
}

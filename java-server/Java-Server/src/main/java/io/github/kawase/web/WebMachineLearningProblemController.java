package io.github.kawase.web;

import io.github.kawase.machinelearning.CreatorMachineLearningProblemService;
import jakarta.servlet.http.HttpServletRequest;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;
import java.util.Map;

@RestController
@RequestMapping("/api/web/ml-problems")
@RequiredArgsConstructor
public class WebMachineLearningProblemController {
    private final WebSessionService webSessionService;
    private final CreatorMachineLearningProblemService creatorProblemService;

    @PostMapping
    public ResponseEntity<CreatorMachineLearningProblemService.CreatorProblemDto> create(
            final HttpServletRequest servletRequest,
            @RequestBody final CreatorMachineLearningProblemService.UpsertProblemRequest request) {
        return ResponseEntity.status(HttpStatus.CREATED)
                .body(creatorProblemService.create(requireParentId(servletRequest), request));
    }

    @GetMapping("/mine")
    public List<CreatorMachineLearningProblemService.CreatorProblemDto> mine(
            final HttpServletRequest servletRequest) {
        return creatorProblemService.getForParent(requireParentId(servletRequest));
    }

    @GetMapping("/children/{childId}/progress")
    public CreatorMachineLearningProblemService.ChildMachineLearningProgressDto childProgress(
            final HttpServletRequest servletRequest,
            @PathVariable final Long childId) {
        return creatorProblemService.getChildProgress(requireParentId(servletRequest), childId);
    }

    @ExceptionHandler(RuntimeException.class)
    public ResponseEntity<Map<String, String>> handleError(final RuntimeException exception) {
        final String message = exception.getMessage() == null ? "Unexpected error" : exception.getMessage();
        final HttpStatus status = switch (message) {
            case "Unauthorized" -> HttpStatus.UNAUTHORIZED;
            case "Access denied" -> HttpStatus.FORBIDDEN;
            case "Child not found", "Parent not found" -> HttpStatus.NOT_FOUND;
            default -> HttpStatus.BAD_REQUEST;
        };
        return ResponseEntity.status(status).body(Map.of("error", message));
    }

    private Long requireParentId(final HttpServletRequest request) {
        final String authorization = request.getHeader("Authorization");
        if (authorization == null || !authorization.startsWith("Bearer "))
            throw new RuntimeException("Unauthorized");
        return webSessionService.requireParentId(authorization.substring("Bearer ".length()).trim());
    }
}

package io.github.kawase.web;

import io.github.kawase.database.services.CourseService;
import jakarta.servlet.http.HttpServletRequest;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;
import java.util.Map;

@RestController
@RequestMapping("/api/web/courses")
@RequiredArgsConstructor
public class WebCourseController {
    private final WebSessionService webSessionService;
    private final CourseService courseService;

    @GetMapping("/mine")
    public List<CourseService.CourseSummaryDto> mine(final HttpServletRequest request) {
        return courseService.getCoursesForParent(requireParentId(request));
    }

    @GetMapping("/{courseId}")
    public CourseService.CourseDetailDto getOne(final HttpServletRequest request, @PathVariable final Long courseId) {
        return courseService.getOwnedCourse(requireParentId(request), courseId);
    }

    @PostMapping
    public CourseService.CourseDetailDto create(
            final HttpServletRequest request,
            @RequestBody final CourseService.UpsertCourseRequest payload) {
        return courseService.createCourseDetail(requireParentId(request), payload);
    }

    @PutMapping("/{courseId}")
    public CourseService.CourseDetailDto update(
            final HttpServletRequest request,
            @PathVariable final Long courseId,
            @RequestBody final CourseService.UpsertCourseRequest payload) {
        return courseService.updateCourseDetail(requireParentId(request), courseId, payload);
    }

    @DeleteMapping("/{courseId}")
    public Map<String, Boolean> delete(final HttpServletRequest request, @PathVariable final Long courseId) {
        courseService.deleteCourse(requireParentId(request), courseId);
        return Map.of("success", true);
    }

    @ExceptionHandler(RuntimeException.class)
    public ResponseEntity<Map<String, String>> handleError(final RuntimeException exception) {
        final String message = exception.getMessage() == null ? "Unexpected error" : exception.getMessage();
        final HttpStatus status = switch (message) {
            case "Unauthorized" -> HttpStatus.UNAUTHORIZED;
            case "Access denied" -> HttpStatus.FORBIDDEN;
            case "Course not found", "Parent not found" -> HttpStatus.NOT_FOUND;
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

package io.github.kawase.web;

import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
public class ApiOnlyController {
    @GetMapping(value = "/", produces = MediaType.TEXT_PLAIN_VALUE)
    public ResponseEntity<String> root() {
        return ResponseEntity.status(HttpStatus.NOT_FOUND)
                .contentType(MediaType.TEXT_PLAIN)
                .body("Mentora backend API only.");
    }
}

FROM gcc:14.2.0-bookworm

COPY run-cpp.sh /opt/mentora/run-cpp.sh
RUN useradd --system --uid 65532 --no-create-home runner && chmod 0555 /opt/mentora/run-cpp.sh

USER 65532:65532
ENTRYPOINT ["/opt/mentora/run-cpp.sh"]

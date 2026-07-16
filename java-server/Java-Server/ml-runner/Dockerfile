FROM python:3.12.4-slim-bookworm

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1

COPY requirements.txt /opt/mentora/requirements.txt
RUN pip install --no-cache-dir --requirement /opt/mentora/requirements.txt

COPY runner.py /opt/mentora/runner.py
RUN useradd --system --uid 65532 --no-create-home runner && chmod 0555 /opt/mentora/runner.py

USER 65532:65532
ENTRYPOINT ["python", "/opt/mentora/runner.py"]

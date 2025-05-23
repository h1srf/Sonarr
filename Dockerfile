FROM lscr.io/linuxserver/sonarr:develop

# Remove the default Sonarr app
RUN rm -rf /app/sonarr

# Copy the custom-built Sonarr files
COPY ./_artifacts/linux-musl-x64/net6.0/Sonarr /app/sonarr/bin

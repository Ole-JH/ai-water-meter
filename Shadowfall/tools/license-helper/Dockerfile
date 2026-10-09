# Temporary Unity Hub desktop, viewable in a web browser (noVNC), used once to obtain a free
# Unity Personal license file without installing anything on the host.
# Run through: task license:activate
FROM debian:bookworm-slim

ENV DEBIAN_FRONTEND=noninteractive LANG=C.UTF-8

RUN apt-get update && apt-get install -y --no-install-recommends \
        ca-certificates curl gnupg \
        xvfb x11vnc fluxbox novnc websockify \
        firefox-esr xdg-utils dbus-x11 gnome-keyring libsecret-1-0 \
        fonts-dejavu-core procps \
    && curl -fsSL https://hub.unity3d.com/linux/keys/public | gpg --dearmor -o /usr/share/keyrings/unityhub.gpg \
    && echo "deb [signed-by=/usr/share/keyrings/unityhub.gpg] https://hub.unity3d.com/linux/repos/deb stable main" \
        > /etc/apt/sources.list.d/unityhub.list \
    && apt-get update && apt-get install -y --no-install-recommends unityhub \
    && rm -rf /var/lib/apt/lists/*

# Containers can't use Chromium's sandbox, so every Unity Hub launch - including the one triggered
# by the unityhub:// sign-in link - goes through a wrapper that adds --no-sandbox.
RUN rm /usr/bin/unityhub \
    && printf '#!/bin/sh\nexec /usr/lib/unityhub/unityhub --no-sandbox --disable-gpu "$@"\n' > /usr/bin/unityhub \
    && chmod +x /usr/bin/unityhub

RUN useradd -m -u 1000 -s /bin/bash unity

# Unity binds licenses to the machine id. Use the same fixed id as GameCI's unityci/editor images
# (see game-ci/docker images/ubuntu/base/Dockerfile) so the license works in the build container.
RUN echo "576562626572264761624c65526f7578" > /etc/machine-id \
    && mkdir -p /var/lib/dbus && ln -sf /etc/machine-id /var/lib/dbus/machine-id
COPY start.sh session.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/start.sh /usr/local/bin/session.sh

EXPOSE 6080
CMD ["start.sh"]

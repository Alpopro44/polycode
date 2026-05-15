import argparse
import asyncio
import logging

from server import PolyCodeServer


def main():
    parser = argparse.ArgumentParser(description="PolyCode Python Engine")
    parser.add_argument("--host", default="127.0.0.1", help="Bind address")
    parser.add_argument("--port", type=int, default=9765, help="WebSocket port")
    parser.add_argument("--debug", action="store_true", help="Enable debug logging")
    args = parser.parse_args()

    logging.basicConfig(
        level=logging.DEBUG if args.debug else logging.INFO,
        format="[PolyCode] %(levelname)s: %(message)s",
    )

    server = PolyCodeServer(host=args.host, port=args.port)
    try:
        asyncio.run(server.start())
    except KeyboardInterrupt:
        print("\nPolyCode Server shut down gracefully.")


if __name__ == "__main__":
    main()

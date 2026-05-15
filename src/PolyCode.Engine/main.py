import asyncio
import logging
from server import PolyCodeServer


def main():
    logging.basicConfig(
        level=logging.INFO,
        format="[%(name)s] %(levelname)s: %(message)s",
    )
    server = PolyCodeServer()
    try:
        asyncio.run(server.start())
    except KeyboardInterrupt:
        print("\nPolyCode Server shut down.")


if __name__ == "__main__":
    main()

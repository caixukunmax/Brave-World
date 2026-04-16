/**
 * ============================================================================
 * Skynet 守护进程模块 - skynet_daemon.c
 * ============================================================================
 * 
 * 【文件作用】
 * 提供守护进程功能，让 Skynet 在后台运行
 * 
 * 【功能】
 * 1. PID 文件管理（防止重复启动）
 * 2. 守护进程化（fork 到后台）
 * 3. 标准输入输出重定向到 /dev/null
 * 
 * 【配置文件】
 * 在 skynet 配置文件中设置：
 * daemon = "./skynet.pid"
 * 
 * 【实现细节】
 * - 使用文件锁保证只有一个实例运行
 * - 支持 macOS（使用 launchd 替代传统 daemon）
 * ============================================================================
 */

#include <stdio.h>
#include <unistd.h>
#include <sys/types.h>
#include <sys/file.h>
#include <signal.h>
#include <errno.h>
#include <stdlib.h>
#include <fcntl.h>

#include "skynet_daemon.h"

/**
 * 【内部】检查 PID 文件，判断进程是否已在运行
 * 
 * @param pidfile  PID 文件路径
 * @return         0 表示没有运行，否则返回正在运行的进程 PID
 */
static int
check_pid(const char *pidfile) {
	int pid = 0;
	FILE *f = fopen(pidfile,"r");
	if (f == NULL)
		return 0;
	int n = fscanf(f,"%d", &pid);
	fclose(f);

	if (n !=1 || pid == 0 || pid == getpid()) {
		return 0;
	}

	// 检查进程是否确实存在
	if (kill(pid, 0) && errno == ESRCH)
		return 0;

	return pid;
}

/**
 * 【内部】写入 PID 文件
 * 
 * @param pidfile  PID 文件路径
 * @return         写入的 PID（0 表示失败）
 */
static int
write_pid(const char *pidfile) {
	FILE *f;
	int pid = 0;
	int fd = open(pidfile, O_RDWR|O_CREAT, 0644);
	if (fd == -1) {
		fprintf(stderr, "Can't create pidfile [%s].\n", pidfile);
		return 0;
	}
	f = fdopen(fd, "w+");
	if (f == NULL) {
		fprintf(stderr, "Can't open pidfile [%s].\n", pidfile);
		return 0;
	}

	// 获取文件锁（非阻塞）
	if (flock(fd, LOCK_EX|LOCK_NB) == -1) {
		int n = fscanf(f, "%d", &pid);
		fclose(f);
		if (n != 1) {
			fprintf(stderr, "Can't lock and read pidfile.\n");
		} else {
			fprintf(stderr, "Can't lock pidfile, lock is held by pid %d.\n", pid);
		}
		return 0;
	}

	pid = getpid();
	if (!fprintf(f,"%d\n", pid)) {
		fprintf(stderr, "Can't write pid.\n");
		close(fd);
		return 0;
	}
	fflush(f);

	return pid;
}

/**
 * 【内部】重定向标准输入输出到 /dev/null
 */
static int
redirect_fds() {
	int nfd = open("/dev/null", O_RDWR);
	if (nfd == -1) {
		perror("Unable to open /dev/null: ");
		return -1;
	}
	if (dup2(nfd, 0) < 0) {
		perror("Unable to dup2 stdin(0): ");
		return -1;
	}
	if (dup2(nfd, 1) < 0) {
		perror("Unable to dup2 stdout(1): ");
		return -1;
	}
	if (dup2(nfd, 2) < 0) {
		perror("Unable to dup2 stderr(2): ");
		return -1;
	}

	close(nfd);

	return 0;
}

/**
 * 【接口】初始化守护进程
 * 
 * @param pidfile  PID 文件路径
 * @return         0 成功，1 失败
 */
int
daemon_init(const char *pidfile) {
	int pid = check_pid(pidfile);

	if (pid) {
		fprintf(stderr, "Skynet is already running, pid = %d.\n", pid);
		return 1;
	}

#ifdef __APPLE__
	// macOS 上 daemon() 已弃用，建议使用 launchd
	fprintf(stderr, "'daemon' is deprecated: first deprecated in OS X 10.5 , use launchd instead.\n");
#else
	// 转为守护进程
	if (daemon(1,1)) {
		fprintf(stderr, "Can't daemonize.\n");
		return 1;
	}
#endif

	pid = write_pid(pidfile);
	if (pid == 0) {
		return 1;
	}

	if (redirect_fds()) {
		return 1;
	}

	return 0;
}

/**
 * 【接口】退出守护进程，删除 PID 文件
 */
int
daemon_exit(const char *pidfile) {
	return unlink(pidfile);
}
